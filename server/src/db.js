const { Pool, types } = require('pg');
const fs = require('fs');
const path = require('path');

// BIGINT (OID 20) as a JS number. Without this, pg returns balances as strings and
// "balance += bid" would concatenate text. Money and times stay far below 2^53.
types.setTypeParser(20, (value) => parseInt(value, 10));

// No default credentials in code: the connection string must come from the environment (.env)
if (!process.env.DATABASE_URL) {
    throw new Error('DATABASE_URL is not set (see server/.env.example)');
}

const pool = new Pool({
    connectionString: process.env.DATABASE_URL,
    max: 20,
    idleTimeoutMillis: 30000
});

async function initDatabase() {
    const schemaPath = path.join(__dirname, '..', 'sql', 'schema.sql');
    const schemaSql = fs.readFileSync(schemaPath, 'utf8');
    await pool.query(schemaSql);
    console.log('[PostgreSQL] Database schema verified and ready.');
}

async function withTransaction(callback) {
    const client = await pool.connect();
    try {
        await client.query('BEGIN');
        const result = await callback(client);
        await client.query('COMMIT');
        return result;
    } catch (err) {
        // A failed ROLLBACK must not hide the original error
        await client.query('ROLLBACK').catch(() => {});
        throw err;
    } finally {
        client.release();
    }
}

module.exports = {
    pool,
    initDatabase,
    withTransaction
};
