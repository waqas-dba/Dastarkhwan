// Dastarkhwan E2E Database Client Helper
// Uses node-postgres (pg) to query the PostgreSQL audit database directly.
// Database: dastarkhwan_runtime_audit | Role: dastarkhwan_audit | Host: 127.0.0.1:5432
// Password is set via PGPASSWORD environment variable (not stored in this file).

const { Pool } = require('pg');

const pool = new Pool({
  host: '127.0.0.1',
  port: 5432,
  user: 'dastarkhwan_audit',
  database: 'dastarkhwan_runtime_audit',
  password: process.env.PGPASSWORD || '',
  max: 5,
  idleTimeoutMillis: 30000,
  connectionTimeoutMillis: 10000,
});

/**
 * Run a SQL query and return rows as array of arrays of column values.
 * @param {string} sql - SQL query
 * @param {Array} params - Query parameters
 * @returns {Array<Array<*>>}
 */
async function query(sql, params = []) {
  const result = await pool.query(sql, params);
  return result.rows.map(row => Object.values(row));
}

/**
 * Run a SQL command (non-query) and return the result.
 * @param {string} sql - SQL command
 * @param {Array} params - Query parameters
 * @returns {Object} pg result
 */
async function execute(sql, params = []) {
  return await pool.query(sql, params);
}

/**
 * Count rows matching a query.
 * @param {string} sql - SELECT count(*) query
 * @param {Array} params - Query parameters
 * @returns {number}
 */
async function count(sql, params = []) {
  const result = await pool.query(sql, params);
  return parseInt(result.rows[0].count, 10);
}

/**
 * Get row count for a table.
 * @param {string} schemaTable - e.g. 'identity."Identity_Users"'
 * @returns {number}
 */
async function tableRowCount(schemaTable) {
  return count(`SELECT COUNT(*) FROM ${schemaTable}`);
}

/**
 * Get a single scalar value from a query.
 * @param {string} sql
 * @param {Array} params
 * @returns {*|null}
 */
async function scalar(sql, params = []) {
  const result = await pool.query(sql, params);
  if (result.rows.length === 0) return null;
  return Object.values(result.rows[0])[0];
}

/**
 * Close the pool (call at end of tests).
 */
async function close() {
  await pool.end();
}

module.exports = {
  query,
  execute,
  count,
  tableRowCount,
  scalar,
  close,
};
