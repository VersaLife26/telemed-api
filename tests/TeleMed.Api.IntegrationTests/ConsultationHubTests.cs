using System.Net;
using System.Text.Json;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Common;
using TeleMed.Application.Consultations;
using static TeleMed.Api.IntegrationTests.Infrastructure.ConsultationFlows;

namespace TeleMed.Api.IntegrationTests;

public class ConsultationHubTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_peers_relay_the_handshake_and_chat_and_a_new_connection_replaces_the_old_one()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        var patientJoin = await patient.Client.JoinedAsync(id);
        var doctorJoin = await doctor.Client.JoinedAsync(id);

        await using var patientPeer = await Factory.ConnectAsync(patientJoin.RoomToken);
        var patientWelcome = (await patientPeer.NextAsync("welcome")).GetProperty("data");
        patientWelcome.GetProperty("role").GetString().ShouldBe("patient");
        patientWelcome.GetProperty("polite").GetBoolean().ShouldBeTrue();
        patientWelcome.GetProperty("peerPresent").GetBoolean().ShouldBeFalse();
        patientWelcome.GetProperty("iceServers")[0].GetProperty("urls")[0].GetString().ShouldBe("stun:stun.cloudflare.com:3478");

        var doctorPeer = await Factory.ConnectAsync(doctorJoin.RoomToken);
        var doctorWelcome = (await doctorPeer.NextAsync("welcome")).GetProperty("data");
        doctorWelcome.GetProperty("polite").GetBoolean().ShouldBeFalse();
        doctorWelcome.GetProperty("peerPresent").GetBoolean().ShouldBeTrue();
        var doctorPeerId = doctorWelcome.GetProperty("peerId").GetString();
        (await patientPeer.NextAsync("peer-joined")).GetProperty("from").GetString().ShouldBe(doctorPeerId);

        await doctorPeer.SendAsync(new { type = "offer", from = "spoofed", data = new { sdp = "v=0 offer" } });
        var offer = await patientPeer.NextAsync("offer");
        offer.GetProperty("from").GetString().ShouldBe(doctorPeerId);
        offer.GetProperty("data").GetProperty("sdp").GetString().ShouldBe("v=0 offer");

        await patientPeer.SendAsync(new { type = "answer", data = new { sdp = "v=0 answer" } });
        (await doctorPeer.NextAsync("answer")).GetProperty("data").GetProperty("sdp").GetString().ShouldBe("v=0 answer");
        await patientPeer.SendAsync(new { type = "ice", data = new { candidate = "candidate:1" } });
        (await doctorPeer.NextAsync("ice")).GetProperty("data").GetProperty("candidate").GetString().ShouldBe("candidate:1");
        await patientPeer.SendAsync(new { type = "chat", data = new { text = "typing..." } });
        (await doctorPeer.NextAsync("chat")).GetProperty("data").GetProperty("text").GetString().ShouldBe("typing...");

        (await patient.Client.PostJsonAsync(Url(id, "/messages"), new { body = "Can you hear me?" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await doctorPeer.NextAsync("chat")).GetProperty("data").GetProperty("body").GetString().ShouldBe("Can you hear me?");
        (await patientPeer.NextAsync("chat")).GetProperty("data").GetProperty("senderRole").GetString().ShouldBe("patient");
        (await (await doctor.Client.GetAsync(Url(id, "/messages"), Ct)).ReadAsync<PagedResult<ConsultationMessageDto>>()).Total.ShouldBe(1);

        await patientPeer.SendAsync(new { type = "ping" });
        await patientPeer.NextAsync("pong");
        await patientPeer.SendAsync(new { type = "recording-state", data = new { recording = true } });
        ErrorCode(await patientPeer.NextAsync("error")).ShouldBe("UNKNOWN_TYPE");
        await patientPeer.SendAsync(new { data = new { sdp = "x" } });
        ErrorCode(await patientPeer.NextAsync("error")).ShouldBe("MALFORMED");

        var replacementJoin = await doctor.Client.JoinedAsync(id);
        await using var replacement = await Factory.ConnectAsync(replacementJoin.RoomToken);
        var replacementWelcome = (await replacement.NextAsync("welcome")).GetProperty("data");
        replacementWelcome.GetProperty("peerPresent").GetBoolean().ShouldBeTrue();
        await doctorPeer.Closed.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        await doctorPeer.DisposeAsync();
        (await patientPeer.NextAsync("peer-joined")).GetProperty("from").GetString().ShouldBe(replacementWelcome.GetProperty("peerId").GetString());

        await patientPeer.SendAsync(new { type = "offer", data = new { sdp = "v=0 restart" } });
        (await replacement.NextAsync("offer")).GetProperty("data").GetProperty("sdp").GetString().ShouldBe("v=0 restart");

        await replacement.Connection.StopAsync(Ct);
        await patientPeer.NextAsync("peer-left");
        await patientPeer.SendAsync(new { type = "offer", data = new { sdp = "nobody" } });
        ErrorCode(await patientPeer.NextAsync("error")).ShouldBe("NO_PEER");

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM consultation_events WHERE kind = 'joined'")).ShouldBe(3);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM consultation_events WHERE kind = 'left'")).ShouldBe(1);
    }

    [Fact]
    public async Task State_changes_and_the_end_of_the_call_are_pushed_to_the_room()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await doctor.Client.InstantMeetingAsync(new { counterpartUserId = patient.UserId });
        var doctorJoin = await doctor.Client.JoinedAsync(id);
        await using var doctorPeer = await Factory.ConnectAsync(doctorJoin.RoomToken);
        await doctorPeer.NextAsync("welcome");

        await patient.Client.JoinedAsync(id);
        (await doctorPeer.NextAsync("state-changed")).GetProperty("data").GetProperty("status").GetString().ShouldBe("waiting");
        await doctor.Client.AdmittedAsync(id);
        (await doctorPeer.NextAsync("state-changed")).GetProperty("data").GetProperty("status").GetString().ShouldBe("active");
        await doctor.Client.EndedAsync(id);
        (await doctorPeer.NextAsync("room-closed")).GetProperty("data").GetProperty("reason").GetString().ShouldBe("ended");

        await using var late = await Factory.ConnectAsync(doctorJoin.RoomToken);
        await late.Closed.WaitAsync(TimeSpan.FromSeconds(15), Ct);
    }

    [Fact]
    public async Task A_forged_room_token_is_refused()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        var token = (await patient.Client.JoinedAsync(id)).RoomToken;
        var forged = token.Replace(".patient.", ".doctor.", StringComparison.Ordinal);

        Peer? peer = null;
        try
        {
            peer = await Factory.ConnectAsync(forged);
        }
        catch (Exception)
        {
        }

        if (peer is not null)
        {
            await peer.Closed.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            await peer.DisposeAsync();
        }

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM consultation_events")).ShouldBe(0);
    }

    private static string? ErrorCode(JsonElement frame) => frame.GetProperty("data").GetProperty("code").GetString();
}
