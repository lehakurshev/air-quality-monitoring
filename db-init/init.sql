CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    email text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    api_token text,
    created_at timestamp without time zone DEFAULT now()
);

CREATE TABLE IF NOT EXISTS measurements (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id uuid REFERENCES users(id) ON DELETE CASCADE,
    pollutants jsonb NOT NULL
);