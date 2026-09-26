import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/widgets/comment_tile.dart';

WorkItemComment _comment(String id, String body) => WorkItemComment(
  id: id,
  workItemId: 'item-1',
  authorUserId: 'user-1',
  authorUsername: 'alice',
  body: body,
  createdAt: DateTime(2026, 9, 26),
  updatedAt: null,
);

Widget _tile(WorkItemComment comment, {ValueChanged<String>? onEdit}) =>
    MaterialApp(
      home: Scaffold(
        body: CommentTile(
          comment: comment,
          isOwnComment: true,
          onDelete: () {},
          onEdit: onEdit ?? (_) {},
        ),
      ),
    );

void main() {
  testWidgets('editing starts from the comment the tile currently shows', (
    tester,
  ) async {
    final edits = <String>[];
    await tester.pumpWidget(_tile(_comment('a', 'First comment')));
    await tester.tap(find.byTooltip('Edit comment'));
    await tester.pump();

    // The same State is reused for a different comment, as an unkeyed list
    // does after the comment above it is deleted.
    await tester.pumpWidget(
      _tile(_comment('b', 'Second comment'), onEdit: edits.add),
    );
    await tester.pump();
    expect(find.byType(TextField), findsNothing);

    await tester.tap(find.byTooltip('Edit comment'));
    await tester.pump();
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'Second comment',
    );

    await tester.tap(find.text('Save'));
    expect(edits, ['Second comment']);
  });
}
