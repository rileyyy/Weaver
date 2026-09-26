/// Weekdays as [DateTime.monday]..[DateTime.sunday], in display order, and
/// their API names (`DayOfWeek` on the backend).
library;

const List<int> weekdaysMondayFirst = [
  DateTime.monday,
  DateTime.tuesday,
  DateTime.wednesday,
  DateTime.thursday,
  DateTime.friday,
  DateTime.saturday,
  DateTime.sunday,
];

const List<String> _wireNames = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

const List<String> _shortNames = [
  'Mon',
  'Tue',
  'Wed',
  'Thu',
  'Fri',
  'Sat',
  'Sun',
];

String weekdayToWire(int weekday) => _wireNames[weekday - 1];

int weekdayFromWire(String value) {
  final index = _wireNames.indexOf(value);
  if (index < 0) throw FormatException('Unknown day of week "$value".');
  return index + 1;
}

String weekdayLetter(int weekday) => _wireNames[weekday - 1][0];

String weekdayShortName(int weekday) => _shortNames[weekday - 1];
