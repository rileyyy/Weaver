import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/header/widgets/time_filter_bar.dart';

void main() {
  testWidgets('each bound can be cleared on its own', (tester) async {
    final changes = <(DateTime?, DateTime?)>[];
    final start = DateTime(2026, 9, 1);
    final end = DateTime(2026, 9, 30);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: TimeFilterBar(
            start: start,
            end: end,
            onChanged: ({DateTime? start, DateTime? end}) =>
                changes.add((start, end)),
            onClear: () {},
          ),
        ),
      ),
    );

    await tester.tap(find.byTooltip('Clear "to" date'));
    await tester.tap(find.byTooltip('Clear "from" date'));

    expect(changes, [(start, null), (null, end)]);
  });
}
