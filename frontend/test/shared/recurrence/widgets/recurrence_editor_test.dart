import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/widgets/recurrence_editor.dart';

Future<List<RecurrenceDraft>> _pump(
  WidgetTester tester,
  RecurrenceDraft draft,
) async {
  final changes = <RecurrenceDraft>[];
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SizedBox(
          // About the narrowest the detail dialog's column gets on a phone.
          width: 280,
          child: RecurrenceEditor(draft: draft, onChanged: changes.add),
        ),
      ),
    ),
  );
  return changes;
}

List<Checkbox> _checkboxes(WidgetTester tester) =>
    tester.widgetList<Checkbox>(find.byType(Checkbox)).toList();

void main() {
  final wednesday = DateTime(2026, 9, 30);

  testWidgets('shows a letter above each of seven weekday checkboxes', (
    tester,
  ) async {
    await _pump(tester, RecurrenceDraft.startingOn(wednesday));

    expect(find.byType(Checkbox), findsNWidgets(7));
    expect(find.text('M'), findsOneWidget);
    expect(find.text('T'), findsNWidgets(2));
    expect(find.text('S'), findsNWidgets(2));
    final values = _checkboxes(tester).map((c) => c.value).toList();
    expect(values, [false, false, true, false, false, false, false]);
    expect(tester.takeException(), isNull);
  });

  testWidgets('ticking a day reports the new selection', (tester) async {
    final changes = await _pump(tester, RecurrenceDraft.startingOn(wednesday));

    await tester.tap(find.byType(Checkbox).first);

    expect(changes.single.weekdays, {DateTime.monday, DateTime.wednesday});
  });

  testWidgets('month-based frequencies disable the weekdays and explain', (
    tester,
  ) async {
    await _pump(
      tester,
      RecurrenceDraft.startingOn(
        wednesday,
      ).withFrequency(RecurrenceFrequency.monthly),
    );

    expect(_checkboxes(tester).every((c) => c.onChanged == null), isTrue);
    expect(find.textContaining('day 30 of the month'), findsOneWidget);
  });

  testWidgets('choosing a frequency reports it', (tester) async {
    final changes = await _pump(tester, RecurrenceDraft.startingOn(wednesday));

    await tester.tap(find.text('Weekly'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Quarterly').last);
    await tester.pumpAndSettle();

    expect(changes.single.frequency, RecurrenceFrequency.quarterly);
  });

  testWidgets('an open-ended repeat says so and can have an end set', (
    tester,
  ) async {
    await _pump(tester, RecurrenceDraft.startingOn(wednesday));

    expect(find.text('No end date'), findsOneWidget);
    expect(find.text('Clear'), findsNothing);
  });

  testWidgets('an end date can be cleared', (tester) async {
    final changes = await _pump(
      tester,
      RecurrenceDraft.startingOn(
        wednesday,
      ).copyWith(endDate: () => DateTime(2026, 12, 31)),
    );

    await tester.tap(find.text('Clear'));

    expect(changes.single.endDate, isNull);
  });
}
