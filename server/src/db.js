const fs = require('fs');
const path = require('path');
const { Pool, types } = require('pg');

// BIGINT (OID 20) as a JS number. Money and times stay far below 2^53.
types.setTypeParser(20, (value) => parseInt(value, 10));

if (!process.env.DATABASE_URL) {
    throw new Error('DATABASE_URL is not set (see server/.env.example)');
}

const pool = new Pool({ connectionString: process.env.DATABASE_URL, max: 10 });

/**
 * Runs fn inside BEGIN/COMMIT. Any throw rolls the whole transaction back, so a
 * half-written trade or race payout can never reach the database.
 */
async function transaction(fn) {
    const client = await pool.connect();
    try {
        await client.query('BEGIN');
        const result = await fn(client);
        await client.query('COMMIT');
        return result;
    } catch (err) {
        await client.query('ROLLBACK').catch(() => {});
        throw err;
    } finally {
        client.release();
    }
}

async function migrate() {
    const sql = fs.readFileSync(path.join(__dirname, '..', 'sql', 'schema.sql'), 'utf8');
    await pool.query(sql);
}

module.exports = { pool, transaction, migrate };
