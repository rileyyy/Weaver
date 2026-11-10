import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/header/done_filter_bar.dart';

void main() {
  Future<void> pumpBar(
    WidgetTester tester, {
    required bool showDone,
    int? hidingDays = 14,
    ValueChanged<bool>? onChanged,
  }) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: DoneFilterBar(
          showDone: showDone,
          hidingDays: hidingDays,
          onChanged: onChanged ?? (_) {},
        ),
      ),
    ),
  );

  bool chipSelected(WidgetTester tester) =>
      tester.widget<FilterChip>(find.byType(FilterChip)).selected;

  testWidgets('off by default, saying what is hidden', (tester) async {
    await pumpBar(tester, showDone: false);

    expect(chipSelected(tester), isFalse);
    expect(
      find.text('Hiding items completed more than 14 days ago.'),
      findsOneWidget,
    );
  });

  testWidgets('a one-day period reads in the singular', (tester) async {
    await pumpBar(tester, showDone: false, hidingDays: 1);

    expect(
      find.text('Hiding items completed more than 1 day ago.'),
      findsOneWidget,
    );
  });

  testWidgets('when on, nothing is hidden so nothing is explained', (
    tester,
  ) async {
    await pumpBar(tester, showDone: true);

    expect(chipSelected(tester), isTrue);
    expect(find.textContaining('Hiding items'), findsNothing);
  });

  testWidgets('tapping the chip toggles Show done', (tester) async {
    final changes = <bool>[];
    await pumpBar(tester, showDone: false, onChanged: changes.add);

    await tester.tap(find.text('Show done'));

    expect(changes, [true]);
  });
}
