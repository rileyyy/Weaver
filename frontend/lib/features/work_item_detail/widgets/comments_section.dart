import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/widgets/comment_tile.dart';
import 'package:weaver/features/work_item_detail/widgets/field_label.dart';
import 'package:weaver/features/work_item_detail/widgets/text_entry_row.dart';

class CommentsSection extends StatelessWidget {
  const CommentsSection({
    super.key,
    required this.comments,
    required this.isOwnComment,
    required this.onAdd,
    required this.onEdit,
    required this.onDelete,
  });

  final List<WorkItemComment> comments;
  final bool Function(WorkItemComment comment) isOwnComment;
  final Future<bool> Function(String body) onAdd;
  final Future<bool> Function(String commentId, String body) onEdit;
  final Future<bool> Function(String commentId) onDelete;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Comments'),
        for (final comment in comments)
          CommentTile(
            key: ValueKey(comment.id),
            comment: comment,
            isOwnComment: isOwnComment(comment),
            onDelete: () => unawaited(onDelete(comment.id)),
            onEdit: (body) => unawaited(onEdit(comment.id, body)),
          ),
        const SizedBox(height: 8),
        TextEntryRow(
          hintText: 'Add a comment',
          buttonLabel: 'Post',
          maxLines: 3,
          onSubmit: onAdd,
        ),
      ],
    );
  }
}
