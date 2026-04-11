/**
 * Single source of truth for every NuGet package version advertised on the website.
 *
 * Override at build time via environment variables (CI):
 *   DATAPROVIDER_VERSION=0.9.7-beta npm run build
 *   LQL_VERSION=0.9.7-beta DATAPROVIDERMIGRATE_VERSION=0.9.7-beta npm run build
 *
 * Every template that references a version MUST use `{{ versions.xxx }}` —
 * never hard-code a version string. The Playwright stale-string sweep enforces this.
 */

const DEFAULT_VERSION = "0.9.6-beta";

module.exports = {
  dataprovider: process.env.DATAPROVIDER_VERSION || DEFAULT_VERSION,
  dataproviderMigrate: process.env.DATAPROVIDERMIGRATE_VERSION || DEFAULT_VERSION,
  lql: process.env.LQL_VERSION || DEFAULT_VERSION,
  nimblesite: process.env.NIMBLESITE_VERSION || DEFAULT_VERSION,
  default: DEFAULT_VERSION,
};
