import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/add_tag_button.dart';
import 'package:weaver/features/board/widgets/board_card.dart';

void main() {
  testWidgets(
    'tapping the add button adds a tag instead of opening the details',
    (tester) async {
      var addTaps = 0;
      var detailOpens = 0;
      await tester.pumpWidget(
        MaterialApp(
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
                onOpenDetails: () => detailOpens++,
                onAssignTapped: () {},
                onAddTagTapped: () => addTaps++,
              ),
            ),
          ),
        ),
      );

      await tester.tap(find.byType(AddTagButton));
      await tester.pump(const Duration(seconds: 1));

      expect(addTaps, 1);
      expect(detailOpens, 0);
    },
  );
}
