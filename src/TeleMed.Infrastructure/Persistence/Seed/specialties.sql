-- Ported from telemed-backend migrations/doctor/000002_doctor_schema.up.sql
INSERT INTO specialties (code, name_en, name_si, name_ta, display_order, is_active, created_at, updated_at)
SELECT v.code, v.name_en, v.name_si, v.name_ta, v.display_order, TRUE, now(), now()
FROM (VALUES
    ('general_practice',  'General Practitioner',       'සාමාන්‍ය වෛද්‍යවරයා',        'பொது மருத்துவர்',            10),
    ('pediatrics',        'Pediatrics',                  'ළමා රෝග',                    'குழந்தை மருத்துவம்',          20),
    ('obstetrics_gynae',  'Obstetrics & Gynaecology',     'ප්‍රසව හා නාරි',              'மகப்பேறு மற்றும் மகளிர் மருத்துவம்', 30),
    ('cardiology',        'Cardiology',                   'හෘද රෝග',                    'இதயவியல்',                    40),
    ('dermatology',       'Dermatology',                  'චර්ම රෝග',                   'தோல் மருத்துவம்',             50),
    ('endocrinology',     'Endocrinology & Diabetes',     'අන්තःස්‍රාවී හා දියවැඩියා',   'நாளமில்சுரப்பியல்',           60),
    ('ent',               'ENT (Ear, Nose & Throat)',     'කන් නාक් උගුරු',             'காது மூக்கு தொண்டை',          70),
    ('psychiatry',        'Psychiatry',                   'මානසික රෝග',                 'மனநல மருத்துவம்',             80),
    ('psychology',        'Psychology & Counselling',     'මනෝ විද්‍යාව',               'உளவியல் ஆலோசனை',              90),
    ('orthopedics',       'Orthopedics',                  'අස්ථි රෝග',                  'எலும்பியல்',                  100),
    ('ophthalmology',     'Ophthalmology (Eye Care)',      'ඇස් රෝග',                    'கண் மருத்துவம்',              110),
    ('neurology',         'Neurology',                    'ස්නායු රෝග',                 'நரம்பியல்',                    120),
    ('gastroenterology',  'Gastroenterology',              'ආමාශ ආන්ත්‍ර රෝග',           'இரைப்பைக் குடலியல்',          130),
    ('nephrology',        'Nephrology',                    'වකුගඩු රෝග',                 'சிறுநீரகவியல்',                140),
    ('urology',           'Urology',                       'මුත්‍රා පද්ධති රෝග',         'சிறுநீர் பாதை மருத்துவம்',     150),
    ('pulmonology',       'Pulmonology (Chest/Lung)',      'පපුව හා පෙනහළු රෝග',         'நுரையீரலியல்',                160),
    ('general_surgery',   'General Surgery',               'සාමාන්‍ය ශල්‍යකර්ම',          'பொது அறுவை சிகிச்சை',          170),
    ('dental',            'Dental',                         'දන්ත වෛද්‍ය',                 'பல் மருத்துவம்',              180),
    ('nutrition',         'Nutrition & Dietetics',          'පෝෂණවේදය',                    'ஊட்டச்சத்து ஆலோசனை',          190)
) AS v(code, name_en, name_si, name_ta, display_order);
