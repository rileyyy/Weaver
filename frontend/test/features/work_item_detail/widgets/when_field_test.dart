import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick.dart';
import 'package:weaver/features/work_item_detail/widgets/when_field.dart';

void main() {
  late List<String> saved;
  late List<QuickPick> picked;

  setUp(() {
    saved = [];
    picked = [];
  });

  Future<void> pump(
    WidgetTester tester, {
    String? when,
    bool resolved = false,
    bool awaitingReResolution = false,
  }) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: WhenField(
          when: when,
          resolved: resolved,
          awaitingReResolution: awaitingReResolution,
          onSave: (text) async {
            saved.add(text);
            return true;
          },
          onQuickPick: (pick) async {
            picked.add(pick);
            return true;
          },
        ),
      ),
    ),
  );

  testWidgets('shows the phrase and that it is unresolved', (tester) async {
    await pump(tester, when: 'after drywall');

    expect(find.text('after drywall'), findsOneWidget);
    expect(find.text('Unresolved'), findsOneWidget);
  });

  testWidgets('shows a phrase backed by dates as resolved', (tester) async {
    await pump(tester, when: 'next week', resolved: true);

    expect(find.text('Resolved'), findsOneWidget);
  });

  testWidgets(
    'shows a phrase edited since it was resolved as awaiting re-resolution',
    (tester) async {
      await pump(
        tester,
        when: 'next summer',
        resolved: true,
        awaitingReResolution: true,
      );

      expect(find.text('Awaiting re-resolution'), findsOneWidget);
      expect(find.text('Resolved'), findsNothing);
    },
  );

  testWidgets('with no phrase, says nothing about resolving', (tester) async {
    await pump(tester);

    expect(find.text('Resolved'), findsNothing);
    expect(find.text('Unresolved'), findsNothing);
  });

  testWidgets('saves the typed phrase on submit', (tester) async {
    await pump(tester);

    await tester.enterText(find.byType(TextField), 'before the market');
    await tester.testTextInput.receiveAction(TextInputAction.done);

    expect(saved, ['before the market']);
  });

  testWidgets('offers every quick pick and passes the tapped one on', (
    tester,
  ) async {
    await pump(tester);

    for (final pick in QuickPick.values) {
      expect(find.widgetWithText(ActionChip, pick.label), findsOneWidget);
    }
    await tester.tap(find.widgetWithText(ActionChip, 'Next month'));

    expect(picked, [QuickPick.nextMonth]);
  });

  testWidgets(
    'a quick pick tapped mid-edit waits for the phrase to save first',
    (tester) async {
      final saveGate = Completer<bool>();
      final events = <String>[];
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: WhenField(
              when: null,
              resolved: false,
              onSave: (text) {
                events.add('save $text');
                return saveGate.future;
              },
              onQuickPick: (pick) async {
                events.add('pick ${pick.name}');
                return true;
              },
            ),
          ),
        ),
      );

      await tester.enterText(find.byType(TextField), 'next week');
      await tester.tap(find.widgetWithText(ActionChip, 'Next week'));
      await tester.pump();
      expect(events, ['save next week']);

      saveGate.complete(true);
      await tester.pump();

      expect(events, ['save next week', 'pick nextWeek']);
    },
  );

  testWidgets('follows a phrase saved elsewhere', (tester) async {
    await pump(tester, when: 'next spring');

    await pump(tester, when: 'after drywall');

    expect(find.text('after drywall'), findsOneWidget);
  });
}
