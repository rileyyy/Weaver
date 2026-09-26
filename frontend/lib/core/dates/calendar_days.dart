/// Day arithmetic on calendar dates. `DateTime.add(Duration(days: n))` adds
/// 24-hour blocks, so across a DST change a local date lands at 23:00 or
/// 01:00, and `difference(...).inDays` truncates the resulting 23 hours to
/// zero days. Both helpers work on the date parts only.
library;

/// Midnight (local) at the start of [date]'s day.
DateTime dateOnly(DateTime date) => DateTime(date.year, date.month, date.day);

/// [date]'s day shifted by [days] calendar days, at local midnight.
DateTime addDays(DateTime date, int days) =>
    DateTime(date.year, date.month, date.day + days);

/// Whole calendar days from [from] to [to] (negative if [to] is earlier).
int daysBetween(DateTime from, DateTime to) => DateTime.utc(
  to.year,
  to.month,
  to.day,
).difference(DateTime.utc(from.year, from.month, from.day)).inDays;
