/// The server's calendar. "Today" is the server's, in its configured time
/// zone, which a phone elsewhere (and an app with no time-zone database)
/// can't work out from its own clock.
abstract class CalendarRepository {
  /// Today's date on the server, at local midnight like every calendar day
  /// the app holds.
  Future<DateTime> today();
}
