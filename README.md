# MaintenanceRag
étape 1: 
J’ai utilisé pgvector, une extension PostgreSQL qui permet de stocker des embeddings (vecteurs) et de faire de la recherche sémantique directement en base.
Concrètement, je transforme les descriptions d’incidents en vecteurs numériques, je les stocke dans une table dédiée, puis je fais une recherche par similarité pour retrouver les incidents les plus proches d’une question utilisateur.
Cela permet d’aller au-delà du simple full-text et de comprendre le sens des requêtes.
