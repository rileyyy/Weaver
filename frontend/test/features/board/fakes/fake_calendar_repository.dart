import 'package:weaver/features/board/data/calendar_repository.dart';

/// A server whose today is [date], or that fails with [error] when set.
class FakeCalendarRepository implements CalendarRepository {
  FakeCalendarRepository([DateTime? date])
    : date = date ?? DateTime(2026, 11, 11);

  DateTime date;
  Exception? error;

  @override
  Future<DateTime> today() async {
    if (error case final error?) throw error;
    return date;
  }
}
