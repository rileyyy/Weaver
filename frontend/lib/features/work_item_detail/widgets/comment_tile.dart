import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';

class CommentTile extends StatefulWidget {
  const CommentTile({
    super.key,
    required this.comment,
    required this.isOwnComment,
    required this.onDelete,
    required this.onEdit,
  });

  final WorkItemComment comment;
  final bool isOwnComment;
  final VoidCallback onDelete;
  final ValueChanged<String> onEdit;

  @override
  State<CommentTile> createState() => _CommentTileState();
}

class _CommentTileState extends State<CommentTile> {
  bool _isEditing = false;
  final TextEditingController _editController = TextEditingController();

  @override
  void didUpdateWidget(CommentTile oldWidget) {
    super.didUpdateWidget(oldWidget);
    // If this State is ever reused for a different comment (e.g. an
    // unkeyed list after a deletion), an open editor holds the old
    // comment's text; saving it would overwrite the new comment.
    if (oldWidget.comment.id != widget.comment.id) _isEditing = false;
  }

  void _startEditing() {
    _editController.text = widget.comment.body;
    setState(() => _isEditing = true);
  }

  @override
  void dispose() {
    _editController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final comment = widget.comment;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '${comment.authorUsername} · ${formatDate(comment.createdAt)}'
                  '${comment.updatedAt != null ? ' (edited)' : ''}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
              if (widget.isOwnComment && !_isEditing) ...[
                IconButton(
                  icon: const Icon(Icons.edit, size: 16),
                  tooltip: 'Edit comment',
                  onPressed: _startEditing,
                ),
                IconButton(
                  icon: const Icon(Icons.delete_outline, size: 16),
                  tooltip: 'Delete comment',
                  onPressed: widget.onDelete,
                ),
              ],
            ],
          ),
          if (_isEditing) ...[
            TextField(controller: _editController, maxLines: 3),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () => setState(() => _isEditing = false),
                  child: const Text('Cancel'),
                ),
                FilledButton(
                  onPressed: () {
                    widget.onEdit(_editController.text);
                    setState(() => _isEditing = false);
                  },
                  child: const Text('Save'),
                ),
              ],
            ),
          ] else
            Text(comment.body),
        ],
      ),
    );
  }
}
