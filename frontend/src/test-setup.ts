// The whole unit test suite runs with the browser in a zone far from the one of the organization (America/Bogota, UTC-5).
// A test of RNF-9 ("the dates follow the organization, not the browser") would otherwise pass by chance on a machine that is
// in Bogotá or farther west, even if the code used the zone of the browser.
process.env['TZ'] = 'Asia/Tokyo';

if (new Date('2026-01-01T00:00:00Z').getTimezoneOffset() !== -9 * 60) {
  throw new Error('The unit tests could not set the zone of the browser to Asia/Tokyo.');
}
