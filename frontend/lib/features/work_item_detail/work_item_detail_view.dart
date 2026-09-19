import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/board/widgets/schedule_dialog.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';

/// Opens [WorkItemDetailView] in a [Dialog] sized to fit comfortably on
/// both desktop and mobile viewports. If [onDrillInto] is given, an app-bar
/// action lets the user close the dialog and re-scope the board to this
/// item's children instead.
Future<void> showWorkItemDetailDialog(
  BuildContext context, {
  required String workItemId,
  VoidCallback? onDrillInto,
}) {
  return showDialog<void>(
    context: context,
    builder: (context) {
      final screenSize = MediaQuery.sizeOf(context);
      return Dialog(
        child: SizedBox(
          width: math.min(560, screenSize.width * 0.95),
          height: math.min(720, screenSize.height * 0.9),
          child: WorkItemDetailView(
            workItemId: workItemId,
            onDrillInto: onDrillInto,
          ),
        ),
      );
    },
  );
}

class WorkItemDetailView extends StatefulWidget {
  const WorkItemDetailView({required this.workItemId, this.onDrillInto, super.key});

  final String workItemId;

  /// When set, shown as an app-bar action that closes this view and hands
  /// control back to the caller to re-scope the board to this item's
  /// children — the same navigation [BoardView.drillInto] performs, just
  /// reachable from the detail dialog instead of a tap on the card itself.
  final VoidCallback? onDrillInto;

  @override
  State<WorkItemDetailView> createState() => _WorkItemDetailViewState();
}

class _WorkItemDetailViewState extends State<WorkItemDetailView> {
  final WorkItemDetailViewModel _viewModel = getIt<WorkItemDetailViewModel>();
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _newCommentController = TextEditingController();
  final _linkTargetController = TextEditingController();
  bool _fieldsInitialized = false;
  String? _selectedLayerId;
  WorkItemPriority _selectedPriority = WorkItemPriority.medium;
  String? _selectedAssigneeId;

  String? get _currentUserId => getIt<AuthSessionStore>().current?.user.id;

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load(widget.workItemId));
    _viewModel.addListener(_onViewModelChanged);
  }

  void _onViewModelChanged() {
    final item = _viewModel.item;
    if (item != null && !_fieldsInitialized) {
      _titleController.text = item.title;
      _descriptionController.text = item.description ?? '';
      _selectedLayerId = item.layerId;
      _selectedPriority = item.priority;
      _selectedAssigneeId = item.assignedToUserId;
      _fieldsInitialized = true;
    }
    setState(() {});
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_onViewModelChanged)
      ..dispose();
    _titleController.dispose();
    _descriptionController.dispose();
    _newCommentController.dispose();
    _linkTargetController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final onDrillInto = widget.onDrillInto;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Work Item Details'),
        actions: [
          if (onDrillInto != null)
            IconButton(
              icon: const Icon(Icons.account_tree_outlined),
              tooltip: 'View sub-items',
              onPressed: () {
                Navigator.of(context).pop();
                onDrillInto();
              },
            ),
        ],
      ),
      body: Builder(
        builder: (context) {
          if (_viewModel.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }
          final loadError = _viewModel.loadError;
          if (loadError != null) {
            return Center(child: Padding(padding: const EdgeInsets.all(16), child: Text(loadError)));
          }
          return _buildForm(context);
        },
      ),
    );
  }

  Widget _buildForm(BuildContext context) {
    final item = _viewModel.item!;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _titleController,
              decoration: const InputDecoration(labelText: 'Title'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _descriptionController,
              decoration: const InputDecoration(labelText: 'Description'),
              maxLines: 4,
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String?>(
              initialValue: _selectedLayerId,
              decoration: const InputDecoration(labelText: 'Layer'),
              items: [
                const DropdownMenuItem(value: null, child: Text('None')),
                for (final layer in _viewModel.layers)
                  DropdownMenuItem(value: layer.id, child: Text(layer.name)),
              ],
              onChanged: (value) => setState(() => _selectedLayerId = value),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<WorkItemPriority>(
              initialValue: _selectedPriority,
              decoration: const InputDecoration(labelText: 'Priority'),
              items: [
                for (final priority in WorkItemPriority.values)
                  DropdownMenuItem(value: priority, child: Text(priority.label)),
              ],
              onChanged: (value) => setState(() => _selectedPriority = value ?? WorkItemPriority.medium),
            ),
            const SizedBox(height: 16),
            if (_viewModel.saveError != null) ...[
              Text(_viewModel.saveError!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 8),
            ],
            FilledButton(
              onPressed: _viewModel.isSaving ? null : () => unawaited(_saveDetails()),
              child: Text(_viewModel.isSaving ? 'Saving…' : 'Save'),
            ),
            const Divider(height: 32),
            Text('Status', style: Theme.of(context).textTheme.labelLarge),
            Text(_viewModel.statusName ?? item.statusId),
            const SizedBox(height: 16),
            Text('Assigned to', style: Theme.of(context).textTheme.labelLarge),
            DropdownButtonFormField<String?>(
              initialValue: _selectedAssigneeId,
              items: [
                const DropdownMenuItem(value: null, child: Text('Unassigned')),
                for (final user in _viewModel.users)
                  DropdownMenuItem(value: user.id, child: Text(user.username)),
              ],
              onChanged: (value) => unawaited(_saveAssignee(value)),
            ),
            const SizedBox(height: 16),
            Text('Schedule', style: Theme.of(context).textTheme.labelLarge),
            Row(
              children: [
                Expanded(child: Text(_scheduleLabel(item.startDate, item.endDate))),
                TextButton(
                  onPressed: () => unawaited(_editSchedule(item.startDate, item.endDate)),
                  child: const Text('Edit'),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Text('Created ${formatDate(item.createdAtUtc)} · Updated ${formatDate(item.updatedAtUtc)}',
                style: Theme.of(context).textTheme.bodySmall),
            const Divider(height: 32),
            _buildLinksSection(context),
            const Divider(height: 32),
            _buildCommentsSection(context),
          ],
        ),
      ),
    );
  }

  Widget _buildLinksSection(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Related work items', style: Theme.of(context).textTheme.labelLarge),
        for (final link in _viewModel.links)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(link.linkedWorkItemTitle),
            trailing: IconButton(
              icon: const Icon(Icons.link_off, size: 18),
              tooltip: 'Remove link',
              onPressed: () => unawaited(_viewModel.deleteLink(link.id)),
            ),
          ),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _linkTargetController,
                decoration: const InputDecoration(hintText: 'Work item id to link'),
              ),
            ),
            const SizedBox(width: 8),
            TextButton(
              onPressed: () => unawaited(_addLink()),
              child: const Text('Add'),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildCommentsSection(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Comments', style: Theme.of(context).textTheme.labelLarge),
        for (final comment in _viewModel.comments) _CommentTile(
          comment: comment,
          isOwnComment: comment.authorUserId == _currentUserId,
          onDelete: () => unawaited(_viewModel.deleteComment(comment.id)),
          onEdit: (body) => unawaited(_viewModel.updateComment(comment.id, body)),
        ),
        const SizedBox(height: 8),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: TextField(
                controller: _newCommentController,
                decoration: const InputDecoration(hintText: 'Add a comment'),
                maxLines: 3,
              ),
            ),
            const SizedBox(width: 8),
            TextButton(
              onPressed: () => unawaited(_addComment()),
              child: const Text('Post'),
            ),
          ],
        ),
      ],
    );
  }

  String _scheduleLabel(DateTime? start, DateTime? end) {
    if (start != null && end != null) return '${formatDate(start)} → ${formatDate(end)}';
    if (start != null) return 'From ${formatDate(start)}';
    if (end != null) return 'Until ${formatDate(end)}';
    return 'Not scheduled';
  }

  Future<void> _saveDetails() async {
    await _viewModel.saveDetails(
      title: _titleController.text,
      description: _descriptionController.text.isEmpty ? null : _descriptionController.text,
      layerId: _selectedLayerId,
      priority: _selectedPriority,
    );
  }

  Future<void> _saveAssignee(String? userId) async {
    final previous = _selectedAssigneeId;
    setState(() => _selectedAssigneeId = userId);
    final ok = await _viewModel.saveAssignee(userId);
    if (!ok && mounted) setState(() => _selectedAssigneeId = previous);
  }

  Future<void> _editSchedule(DateTime? start, DateTime? end) async {
    final result = await showScheduleDialog(context, initialStart: start, initialEnd: end);
    if (result == null) return;
    await _viewModel.saveSchedule(result.startDate, result.endDate);
  }

  Future<void> _addComment() async {
    final body = _newCommentController.text.trim();
    if (body.isEmpty) return;
    final ok = await _viewModel.addComment(body);
    if (ok) _newCommentController.clear();
  }

  Future<void> _addLink() async {
    final targetId = _linkTargetController.text.trim();
    if (targetId.isEmpty) return;
    final ok = await _viewModel.addLink(targetId);
    if (ok) _linkTargetController.clear();
  }
}

class _CommentTile extends StatefulWidget {
  const _CommentTile({
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
  State<_CommentTile> createState() => _CommentTileState();
}

class _CommentTileState extends State<_CommentTile> {
  bool _isEditing = false;
  late final _editController = TextEditingController(text: widget.comment.body);

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
                  '${comment.authorUsername} · ${formatDate(comment.createdAtUtc)}'
                  '${comment.updatedAtUtc != null ? ' (edited)' : ''}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
              if (widget.isOwnComment && !_isEditing) ...[
                IconButton(
                  icon: const Icon(Icons.edit, size: 16),
                  tooltip: 'Edit comment',
                  onPressed: () => setState(() => _isEditing = true),
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
