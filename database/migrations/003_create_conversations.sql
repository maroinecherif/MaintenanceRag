-- Create conversations table for storing Q&A history
CREATE TABLE IF NOT EXISTS conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question TEXT NOT NULL,
    answer TEXT,
    equipment TEXT,
    sources UUID[] DEFAULT ARRAY[]::UUID[],
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Index for faster retrieval by date
CREATE INDEX IF NOT EXISTS idx_conversations_created_at 
ON conversations(created_at DESC);

-- Sample data
INSERT INTO conversations (question, answer, equipment, sources) VALUES
(
    'Quels sont les problèmes courants des pompes hydrauliques ?',
    'Les pompes hydrauliques rencontrent souvent des fuites, des usures et des surcharges.',
    'pompe',
    ARRAY['550e8400-e29b-41d4-a716-446655440001'::UUID, '550e8400-e29b-41d4-a716-446655440002'::UUID]
),
(
    'Comment entretenir les compresseurs ?',
    'L''entretien régulier inclut le nettoyage des filtres et la vérification de la pression.',
    'compresseur',
    ARRAY['550e8400-e29b-41d4-a716-446655440003'::UUID]
);
