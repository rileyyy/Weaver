import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/header/tag_filter_bar.dart';
import 'package:weaver/shared/models/tag_match.dart';

void main() {
  Future<void> pumpBar(
    WidgetTester tester, {
    required Set<String> selected,
    TagMatch tagMatch = TagMatch.any,
    ValueChanged<TagMatch>? onTagMatchChanged,
  }) {
    return tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: TagFilterBar(
            availableTags: const ['bug', 'ui', 'backend'],
            isSelected: selected.contains,
            onToggle: (_) {},
            tagMatch: tagMatch,
            onTagMatchChanged: onTagMatchChanged ?? (_) {},
          ),
        ),
      ),
    );
  }

  final matchControl = find.byType(SegmentedButton<TagMatch>);

  testWidgets('hides the match control with no tags selected', (tester) async {
    await pumpBar(tester, selected: {});

    expect(matchControl, findsNothing);
  });

  testWidgets('hides the match control with one tag selected', (tester) async {
    await pumpBar(tester, selected: {'bug'});

    expect(matchControl, findsNothing);
  });

  testWidgets('shows the match control with two tags selected', (tester) async {
    await pumpBar(tester, selected: {'bug', 'ui'});

    expect(matchControl, findsOneWidget);
    expect(find.text('Any'), findsOneWidget);
    expect(find.text('All'), findsOneWidget);
  });

  testWidgets('shows the current match as selected', (tester) async {
    await pumpBar(tester, selected: {'bug', 'ui'}, tagMatch: TagMatch.all);

    final button = tester.widget<SegmentedButton<TagMatch>>(matchControl);
    expect(button.selected, {TagMatch.all});
  });

  testWidgets('tapping the other option reports it', (tester) async {
    final changes = <TagMatch>[];
    await pumpBar(
      tester,
      selected: {'bug', 'ui'},
      onTagMatchChanged: changes.add,
    );

    await tester.tap(find.text('All'));

    expect(changes, [TagMatch.all]);
  });
}
