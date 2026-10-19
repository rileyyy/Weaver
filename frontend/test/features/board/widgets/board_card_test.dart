import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/add_tag_button.dart';
import 'package:weaver/features/board/widgets/board_card.dart';

Widget _card({
  Color? layerColor,
  VoidCallback? onOpenDetails,
  VoidCallback? onAddTagTapped,
}) => MaterialApp(
  home: Scaffold(
    body: SizedBox(
      width: 208,
      child: BoardCard(
        card: const WorkItemCard(
          id: 'card-1',
          number: 12,
          title: 'Setup CI',
          parentId: 'lane-1',
          statusId: 'done',
        ),
        height: 120,
        assigneeInitial: null,
        layerColor: layerColor,
        onOpenDetails: onOpenDetails ?? () {},
        onAssignTapped: () {},
        onAddTagTapped: onAddTagTapped ?? () {},
      ),
    ),
  ),
);

/// The colour of the bar along the card's left edge.
Color? _barColor(WidgetTester tester) {
  final row = find.descendant(
    of: find.byType(Card),
    matching: find.byType(Row),
  );
  final bar = find.descendant(of: row.first, matching: find.byType(Container));
  return tester.widget<Container>(bar.first).color;
}

void main() {
  testWidgets(
    'tapping the add button adds a tag instead of opening the details',
    (tester) async {
      var addTaps = 0;
      var detailOpens = 0;
      await tester.pumpWidget(
        _card(
          onOpenDetails: () => detailOpens++,
          onAddTagTapped: () => addTaps++,
        ),
      );

      await tester.tap(find.byType(AddTagButton));
      await tester.pump(const Duration(seconds: 1));

      expect(addTaps, 1);
      expect(detailOpens, 0);
    },
  );

  testWidgets("the left bar shows the card's layer colour", (tester) async {
    await tester.pumpWidget(_card(layerColor: const Color(0xFFD81B60)));

    expect(_barColor(tester), const Color(0xFFD81B60));
  });

  testWidgets('a card without a layer gets a neutral bar', (tester) async {
    await tester.pumpWidget(_card());

    final context = tester.element(find.byType(BoardCard));
    expect(_barColor(tester), Theme.of(context).colorScheme.outline);
  });
}
