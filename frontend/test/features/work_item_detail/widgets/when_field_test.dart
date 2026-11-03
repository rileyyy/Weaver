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
  }) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: WhenField(
          when: when,
          resolved: resolved,
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

  testWidgets('follows a phrase saved elsewhere', (tester) async {
    await pump(tester, when: 'next spring');

    await pump(tester, when: 'after drywall');

    expect(find.text('after drywall'), findsOneWidget);
  });
}
