/// Short, phone-friendly date labels for the Agenda, e.g. `Thu 13 Nov`.
library;

import 'package:weaver/core/dates/calendar_days.dart';

const _weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const _months = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

/// [day] as `Thu 13 Nov`, with the year when it isn't [today]'s.
String agendaDate(DateTime day, {required DateTime today}) {
  final label =
      '${_weekdays[day.weekday - 1]} ${day.day} ${_months[day.month - 1]}';
  return day.year == today.year ? label : '$label ${day.year}';
}

/// The heading of the Agenda group for [day]: Overdue when null, then
/// Today, Tomorrow, or the date.
String agendaHeading(DateTime? day, {required DateTime today}) {
  if (day == null) return 'Overdue';
  return switch (daysBetween(today, day)) {
    0 => 'Today',
    1 => 'Tomorrow',
    _ => agendaDate(day, today: today),
  };
}

/// When an item is on, or null when that's just [day], the heading it
/// shows under, so the heading already says it.
String? agendaRange(
  DateTime? start,
  DateTime? end, {
  required DateTime? day,
  required DateTime today,
}) {
  String date(DateTime date) => agendaDate(date, today: today);
  return switch ((start, end)) {
    (final start?, final end?) when daysBetween(start, end) == 0 =>
      day != null && daysBetween(start, day) == 0 ? null : date(start),
    (final start?, final end?) => '${date(start)} – ${date(end)}',
    (final start?, null) => 'From ${date(start)}',
    (null, final end?) => 'Until ${date(end)}',
    (null, null) => null,
  };
}
