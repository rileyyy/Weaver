import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/repeating_items_view_model.dart';
import 'package:weaver/features/board/views/repeating/repeating_view.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';

import '../../../../shared/recurrence/fake_recurrence_repository.dart';

Future<RepeatingItemsViewModel> _pump(
  WidgetTester tester,
  FakeRecurrenceRepository repository, {
  List<String>? opened,
  List<void>? changed,
}) async {
  final viewModel = RepeatingItemsViewModel(repository);
  addTearDown(viewModel.dispose);
  await viewModel.load();
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: RepeatingView(
          viewModel: viewModel,
          onItemOpened: (id) => opened?.add(id),
          onSchedulesChanged: () => changed?.add(null),
        ),
      ),
    ),
  );
  return viewModel;
}

void main() {
  testWidgets('lists each repeating item with its schedule', (tester) async {
    await _pump(
      tester,
      FakeRecurrenceRepository([
        recurrence(
          number: 7,
          title: 'Weekly report',
          parentTitle: 'Finance',
          weekdays: const {DateTime.monday, DateTime.friday},
          nextOccurrence: DateTime(2026, 10, 2),
        ),
        recurrence(
          workItemId: 'item-2',
          number: 8,
          title: 'Budget review',
          frequency: RecurrenceFrequency.quarterly,
          weekdays: const {},
          startDate: DateTime(2026, 1, 15),
          endDate: DateTime(2026, 12, 31),
        ),
      ]),
    );

    expect(find.text('#7 Weekly report'), findsOneWidget);
    expect(
      find.textContaining('In Finance · Weekly on Mon, Fri'),
      findsOneWidget,
    );
    expect(
      find.textContaining('no end date · Next 2026-10-02'),
      findsOneWidget,
    );
    expect(find.textContaining('Quarterly on day 15'), findsOneWidget);
    expect(find.textContaining('2026-01-15 → 2026-12-31'), findsOneWidget);
  });

  testWidgets('says how to add one when there are none', (tester) async {
    await _pump(tester, FakeRecurrenceRepository());

    expect(find.textContaining('No repeating items yet'), findsOneWidget);
  });

  testWidgets('tapping a row opens the item', (tester) async {
    final opened = <String>[];
    await _pump(
      tester,
      FakeRecurrenceRepository([recurrence()]),
      opened: opened,
    );

    await tester.tap(find.text('#1 Report'));

    expect(opened, ['item-1']);
  });

  testWidgets('editing the end date saves it and reports the change', (
    tester,
  ) async {
    final repository = FakeRecurrenceRepository([recurrence()]);
    final changed = <void>[];
    await _pump(tester, repository, changed: changed);

    await tester.tap(find.byTooltip('Edit schedule'));
    await tester.pumpAndSettle();
    final save = find.widgetWithText(FilledButton, 'Save');
    expect(tester.widget<FilledButton>(save).onPressed, isNull);

    await tester.tap(find.text('Edit').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('30'));
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();
    await tester.tap(save);
    await tester.pumpAndSettle();

    expect(repository.saveCalls.single.$2.endDate, DateTime(2026, 9, 30));
    expect(changed, hasLength(1));
    expect(find.textContaining('2026-09-28 → 2026-09-30'), findsOneWidget);
  });

  testWidgets('stopping asks first, then removes the row', (tester) async {
    final repository = FakeRecurrenceRepository([recurrence()]);
    final changed = <void>[];
    await _pump(tester, repository, changed: changed);

    await tester.tap(find.byTooltip('Stop repeating'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Stop repeating'));
    await tester.pumpAndSettle();

    expect(repository.removeCalls, ['item-1']);
    expect(changed, hasLength(1));
    expect(find.textContaining('No repeating items yet'), findsOneWidget);
  });
}
