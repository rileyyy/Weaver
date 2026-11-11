import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/agenda/agenda_labels.dart';

/// Wednesday 11 November 2026.
final _today = DateTime(2026, 11, 11);

void main() {
  group('agendaHeading', () {
    test('names Overdue, Today and Tomorrow, then the date', () {
      expect(agendaHeading(null, today: _today), 'Overdue');
      expect(agendaHeading(DateTime(2026, 11, 11), today: _today), 'Today');
      expect(agendaHeading(DateTime(2026, 11, 12), today: _today), 'Tomorrow');
      expect(
        agendaHeading(DateTime(2026, 11, 13), today: _today),
        'Fri 13 Nov',
      );
    });

    test('adds the year to a date in another year', () {
      expect(
        agendaHeading(DateTime(2027, 1, 4), today: _today),
        'Mon 4 Jan 2027',
      );
    });
  });

  group('agendaRange', () {
    test('is nothing for a one-day item under its own day', () {
      final day = DateTime(2026, 11, 13);

      expect(agendaRange(day, day, day: day, today: _today), isNull);
    });

    test('is the date of a one-day item under another heading', () {
      final day = DateTime(2026, 11, 4);

      expect(agendaRange(day, day, day: null, today: _today), 'Wed 4 Nov');
    });

    test('spans the first to the last day of a multi-day item', () {
      expect(
        agendaRange(
          DateTime(2026, 11, 9),
          DateTime(2026, 11, 13),
          day: _today,
          today: _today,
        ),
        'Mon 9 Nov – Fri 13 Nov',
      );
    });

    test('says From or Until for an item with one open side', () {
      expect(
        agendaRange(DateTime(2026, 10, 1), null, day: _today, today: _today),
        'From Thu 1 Oct',
      );
      expect(
        agendaRange(null, DateTime(2026, 11, 14), day: _today, today: _today),
        'Until Sat 14 Nov',
      );
    });
  });
}
