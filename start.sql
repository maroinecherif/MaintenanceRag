-- =============================================================
-- SCRIPT D'INITIALISATION - BASE DE DONNÉES MaintenanceRag
-- PostgreSQL + pgvector
-- =============================================================

-- -------------------------------------------------------------
-- 1. CONNEXION À LA BASE
-- -------------------------------------------------------------
\connect MaintenanceRag;

-- -------------------------------------------------------------
-- 2. EXTENSION PGVECTOR
-- -------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS vector;

-- -------------------------------------------------------------
-- 3. TABLE INCIDENTS
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS incidents (
    id               UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    equipment_name   VARCHAR(200)  NOT NULL,
    incident_date    TIMESTAMP     NOT NULL,
    description      TEXT          NOT NULL,
    cause            TEXT          NULL,
    solution         TEXT          NULL,
    search_text      TEXT          NOT NULL
);

-- -------------------------------------------------------------
-- 4. COLONNE GÉNÉRÉE search_vector (tsvector full-text)
-- -------------------------------------------------------------
ALTER TABLE incidents
    ADD COLUMN IF NOT EXISTS search_vector TSVECTOR
        GENERATED ALWAYS AS (
            to_tsvector('french',
                coalesce(equipment_name, '') || ' ' ||
                coalesce(description,    '') || ' ' ||
                coalesce(cause,          '') || ' ' ||
                coalesce(solution,       '')
            )
        ) STORED;

-- -------------------------------------------------------------
-- 5. INDEX GIN SUR search_vector
-- -------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_incidents_search_vector
    ON incidents USING GIN (search_vector);

-- -------------------------------------------------------------
-- 6. TABLE INCIDENT_EMBEDDINGS (pgvector 384 dimensions)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS incident_embeddings (
    incident_id UUID          PRIMARY KEY
                              REFERENCES incidents(id) ON DELETE CASCADE,
    embedding   VECTOR(384)   NOT NULL
);

-- -------------------------------------------------------------
-- 7. INDEX IVFFLAT SUR LES EMBEDDINGS (recherche ANN)
-- -------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_incident_embeddings_ivfflat
    ON incident_embeddings USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 50);

-- =============================================================
-- 8. DONNÉES FAKE - 20+ INCIDENTS RÉALISTES EN FRANÇAIS
-- =============================================================

INSERT INTO incidents (id, equipment_name, incident_date, description, cause, solution, search_text) VALUES

-- ── POMPES HYDRAULIQUES ──────────────────────────────────────
(
    'a1b2c3d4-0001-0001-0001-000000000001',
    'Pompe hydraulique HP-201',
    '2024-01-15 08:30:00',
    'Fuite importante d''huile hydraulique détectée au niveau du joint de tige du piston.',
    'Usure prématurée du joint d''étanchéité due à une contamination de l''huile par des particules métalliques.',
    'Remplacement du joint de tige et vidange complète du circuit hydraulique. Ajout d''un filtre à particules 10 microns.',
    'pompe hydraulique HP-201 fuite huile joint tige piston usure contamination remplacement filtre'
),
(
    'a1b2c3d4-0002-0002-0002-000000000002',
    'Pompe hydraulique HP-305',
    '2024-02-03 14:15:00',
    'Perte de pression significative dans le circuit hydraulique principal, chute de 250 bar à 180 bar.',
    'Clapet anti-retour défaillant laissant refluer le fluide vers le réservoir.',
    'Remplacement du clapet anti-retour et recalibration de la soupape de surpression à 250 bar.',
    'pompe hydraulique HP-305 perte pression circuit clapet anti-retour reflux soupape surpression recalibration'
),
(
    'a1b2c3d4-0003-0003-0003-000000000003',
    'Pompe hydraulique HP-201',
    '2024-03-20 09:00:00',
    'Surchauffe de la pompe hydraulique, température atteinte 92°C pour un seuil admissible de 75°C.',
    'Échangeur thermique encrassé réduisant le refroidissement du fluide hydraulique.',
    'Nettoyage chimique de l''échangeur thermique et vérification du débit d''eau de refroidissement.',
    'pompe hydraulique HP-201 surchauffe température échangeur thermique encrassé refroidissement nettoyage'
),
(
    'a1b2c3d4-0004-0004-0004-000000000004',
    'Pompe hydraulique HP-410',
    '2024-05-10 11:45:00',
    'Vibrations anormales et bruit de cavitation lors du démarrage à froid.',
    'Niveau d''huile insuffisant dans le réservoir causant une aspiration d''air dans le circuit.',
    'Appoint d''huile hydraulique ISO VG 46 et purge complète du circuit. Vérification de l''étanchéité du circuit d''aspiration.',
    'pompe hydraulique HP-410 vibrations cavitation démarrage huile niveau aspiration air purge circuit'
),

-- ── COMPRESSEURS ─────────────────────────────────────────────
(
    'a1b2c3d4-0005-0005-0005-000000000005',
    'Compresseur à vis CX-100',
    '2024-01-28 07:00:00',
    'Arrêt automatique du compresseur sur défaut haute température, alarme T > 110°C.',
    'Filtre à huile colmaté empêchant la lubrification correcte des rotors.',
    'Remplacement du filtre à huile et de l''huile compresseur. Nettoyage du refroidisseur intermédiaire.',
    'compresseur vis CX-100 arrêt haute température filtre huile colmaté lubrification rotors refroidisseur'
),
(
    'a1b2c3d4-0006-0006-0006-000000000006',
    'Compresseur à pistons CP-220',
    '2024-02-14 16:20:00',
    'Pression de refoulement instable, oscillations entre 6 et 9 bar au lieu de 8 bar stables.',
    'Soupape de refoulement du 2ème étage présentant une fuite interne par usure des sièges.',
    'Remplacement de la soupape de refoulement 2ème étage et vérification de toutes les soupapes d''aspiration.',
    'compresseur pistons CP-220 pression refoulement instable soupape usure siège aspiration 2ème étage'
),
(
    'a1b2c3d4-0007-0007-0007-000000000007',
    'Compresseur à vis CX-350',
    '2024-04-05 10:30:00',
    'Défaut de démarrage répété, le compresseur ne passe pas en mode charge après 3 tentatives.',
    'Pressostat de pression minimale dessoudé sur la carte électronique de contrôle.',
    'Remplacement de la carte électronique de contrôle et reconfiguration des paramètres de pression.',
    'compresseur vis CX-350 démarrage défaut pressostat carte électronique contrôle pression minimale'
),
(
    'a1b2c3d4-0008-0008-0008-000000000008',
    'Compresseur à vis CX-100',
    '2024-06-18 13:00:00',
    'Fuite d''air comprimé importante au niveau du raccord de sortie du séparateur air/huile.',
    'Joint torique du raccord de sortie dégradé par vieillissement et cycles thermiques répétés.',
    'Remplacement du joint torique et serrage au couple préconisé. Contrôle de l''ensemble des raccords du circuit.',
    'compresseur vis CX-100 fuite air comprimé raccord séparateur joint torique vieillissement thermique'
),

-- ── CONVOYEURS ───────────────────────────────────────────────
(
    'a1b2c3d4-0009-0009-0009-000000000009',
    'Convoyeur à bande CB-01',
    '2024-01-10 06:00:00',
    'Désalignement de la bande transporteuse provoquant un contact avec le châssis et usure rapide du bord.',
    'Rouleaux de retour mal positionnés suite à une intervention de maintenance précédente.',
    'Réalignement de la bande par ajustement des rouleaux porteurs et de retour. Mise en place de capteurs de désalignement.',
    'convoyeur bande CB-01 désalignement rouleaux retour porteurs châssis usure capteur alignement'
),
(
    'a1b2c3d4-0010-0010-0010-000000000010',
    'Convoyeur à rouleaux CR-15',
    '2024-03-08 08:45:00',
    'Blocage du convoyeur par coincement d''une pièce entre deux rouleaux, arrêt d''urgence déclenché.',
    'Absence de détecteur de bourrage et écartement entre rouleaux non conforme aux gabarits des pièces.',
    'Recalibrage de l''écartement des rouleaux, installation d''un détecteur de bourrage et ajout d''un carter de protection.',
    'convoyeur rouleaux CR-15 blocage coincement bourrage détecteur écartement gabarit carter protection'
),
(
    'a1b2c3d4-0011-0011-0011-000000000011',
    'Convoyeur à bande CB-03',
    '2024-05-25 14:30:00',
    'Bruit de craquement anormal au niveau du tambour de tête, vibrations transmises au châssis.',
    'Roulement du tambour de tête en fin de vie, billes écrasées détectées à l''analyse vibratoire.',
    'Remplacement du roulement du tambour de tête en profitant de l''arrêt planifié du week-end.',
    'convoyeur bande CB-03 bruit craquement tambour tête vibrations roulement fin vie analyse vibratoire'
),
(
    'a1b2c3d4-0012-0012-0012-000000000012',
    'Convoyeur à chaîne CC-07',
    '2024-07-02 09:15:00',
    'Rupture d''un maillon de chaîne entraînant l''arrêt complet de la ligne de production.',
    'Lubrification insuffisante de la chaîne entraînant une usure accélérée des maillons.',
    'Remplacement de la chaîne complète, mise en place d''un système de lubrification automatique centralisé.',
    'convoyeur chaîne CC-07 rupture maillon arrêt ligne production lubrification usure système automatique'
),

-- ── MOTEURS ÉLECTRIQUES ──────────────────────────────────────
(
    'a1b2c3d4-0013-0013-0013-000000000013',
    'Moteur électrique ME-55kW',
    '2024-02-20 10:00:00',
    'Surchauffe du moteur électrique, protection thermique déclenchée à 155°C sur le bobinage stator.',
    'Tension d''alimentation déséquilibrée entre phases (L1: 400V, L2: 385V, L3: 395V), causant des courants de déséquilibre.',
    'Correction du déséquilibre de tension par intervention sur le tableau de distribution. Vérification des connexions au bornier moteur.',
    'moteur électrique ME-55kW surchauffe protection thermique bobinage stator déséquilibre tension phases tableau distribution'
),
(
    'a1b2c3d4-0014-0014-0014-000000000014',
    'Moteur électrique ME-22kW',
    '2024-04-12 07:30:00',
    'Vibrations excessives du moteur transmises au réducteur, niveau vibratoire 14 mm/s RMS (seuil 7 mm/s).',
    'Désalignement angulaire entre l''arbre moteur et l''arbre d''entrée du réducteur, écart de 0,8 mm.',
    'Réalignement laser de l''accouplement moteur-réducteur, remplacement du manchon d''accouplement souple.',
    'moteur électrique ME-22kW vibrations réducteur désalignement angulaire arbre accouplement laser manchon'
),
(
    'a1b2c3d4-0015-0015-0015-000000000015',
    'Moteur électrique ME-75kW',
    '2024-06-01 15:45:00',
    'Défaut d''alimentation électrique, disjoncteur magnétothermique déclenché sur surintensité.',
    'Court-circuit partiel dans le bobinage stator suite à une dégradation de l''isolation par humidité.',
    'Rebobinage complet du stator par un atelier spécialisé. Mise en place d''une protection contre l''humidité (réchauffeur antigel).',
    'moteur électrique ME-75kW défaut alimentation disjoncteur surintensité court-circuit bobinage isolation humidité rebobinage'
),
(
    'a1b2c3d4-0016-0016-0016-000000000016',
    'Moteur électrique ME-11kW',
    '2024-07-14 08:00:00',
    'Capteur de température PTC défaillant, signal constant à 0°C alors que le moteur est en fonctionnement.',
    'Rupture du câble du capteur PTC dans le presse-étoupe suite à des vibrations chroniques.',
    'Remplacement du câble capteur PTC et sécurisation du câblage dans le conduit. Remplacement du presse-étoupe.',
    'moteur électrique ME-11kW capteur température PTC défaillant signal rupture câble presse-étoupe vibrations'
),

-- ── GROUPES FROIDS ───────────────────────────────────────────
(
    'a1b2c3d4-0017-0017-0017-000000000017',
    'Groupe froid GF-200',
    '2024-01-22 11:00:00',
    'Perte de pression réfrigérant dans le circuit frigorifique, le groupe ne maintient plus la consigne de -5°C.',
    'Fuite de réfrigérant R410A au niveau du raccord brasé sur le détendeur thermostatique.',
    'Réparation de la fuite par rebragage du raccord, recharge en réfrigérant R410A (1,2 kg) et contrôle d''étanchéité.',
    'groupe froid GF-200 perte pression réfrigérant R410A fuite détendeur thermostatique brasure recharge étanchéité'
),
(
    'a1b2c3d4-0018-0018-0018-000000000018',
    'Groupe froid GF-350',
    '2024-03-15 13:30:00',
    'Givrage de l''évaporateur bloquant l''échange thermique, défaut dégivrage signalé par l''automate.',
    'Résistance de dégivrage de l''évaporateur hors service (circuit ouvert mesuré à 240V).',
    'Remplacement de la résistance de dégivrage et vérification du thermostat de dégivrage et du minuterie.',
    'groupe froid GF-350 givrage évaporateur échange thermique dégivrage résistance circuit ouvert thermostat minuterie'
),
(
    'a1b2c3d4-0019-0019-0019-000000000019',
    'Groupe froid GF-200',
    '2024-05-30 09:30:00',
    'Surchauffe du compresseur frigorifique, clixon de protection déclenché à 3 reprises en 24h.',
    'Condenseur encrassé par accumulation de poussières réduisant le transfert thermique de 40%.',
    'Nettoyage haute pression du condenseur et soufflage des ailettes. Planification nettoyage trimestriel.',
    'groupe froid GF-200 surchauffe compresseur frigorifique clixon condenseur encrassé poussière ailettes nettoyage haute pression'
),
(
    'a1b2c3d4-0020-0020-0020-000000000020',
    'Groupe froid GF-500',
    '2024-06-25 16:00:00',
    'Capteur de pression haute pression défaillant, valeur figée à 18 bar en affichage alors que la pression mesurée est de 24 bar.',
    'Capteur 4-20mA en dérive totale, membrane interne endommagée par un coup de bélier hydraulique.',
    'Remplacement du capteur de pression HP (0-40 bar, 4-20mA), recalibration de l''afficheur et test fonctionnel.',
    'groupe froid GF-500 capteur pression haute pression défaillant dérive membrane coup de bélier remplacement calibration'
),
(
    'a1b2c3d4-0021-0021-0021-000000000021',
    'Compresseur à vis CX-220',
    '2024-08-05 07:15:00',
    'Vibrations anormales et bruit métallique sur le compresseur à vis, arrêt préventif décidé par l''opérateur.',
    'Roulement avant de la vis mâle détérioré, traces de métal blanc dans le filtre à huile.',
    'Remplacement des roulements avant et arrière des deux vis, vidange et analyse spectrale de l''huile.',
    'compresseur vis CX-220 vibrations bruit roulement vis mâle métal filtre huile remplacement analyse spectrale'
),
(
    'a1b2c3d4-0022-0022-0022-000000000022',
    'Pompe hydraulique HP-305',
    '2024-09-10 10:00:00',
    'Bruit de claquement intermittent à la pompe lors des variations de charge, cavitation suspectée.',
    'Filtre d''aspiration partiellement colmaté réduisant le débit d''alimentation en huile froide.',
    'Remplacement du filtre d''aspiration et vérification de la vanne d''isolement du réservoir (partiellement fermée).',
    'pompe hydraulique HP-305 bruit claquement cavitation filtre aspiration colmaté débit huile vanne réservoir'
);

-- =============================================================
-- 9. REQUÊTES SQL DE TEST
-- =============================================================

-- ── TEST 1 : Vérification des 5 premiers incidents ──────────
SELECT
    id,
    equipment_name,
    incident_date,
    LEFT(description, 80) AS description_courte,
    LEFT(solution,     60) AS solution_courte
FROM incidents
LIMIT 5;

-- ── TEST 2 : Recherche full-text "pompe hydraulique pression" ─
SELECT
    id,
    equipment_name,
    incident_date,
    ts_rank(search_vector, query) AS score,
    LEFT(description, 100) AS description
FROM
    incidents,
    to_tsquery('french', 'pompe & hydraulique & pression') AS query
WHERE
    search_vector @@ query
ORDER BY
    score DESC;

-- ── TEST 3 : Jointure incidents ↔ incident_embeddings ────────
SELECT
    i.id,
    i.equipment_name,
    i.incident_date,
    LEFT(i.description, 80) AS description,
    ie.incident_id          AS embedding_disponible
FROM incidents i
INNER JOIN incident_embeddings ie ON ie.incident_id = i.id
ORDER BY i.incident_date DESC;