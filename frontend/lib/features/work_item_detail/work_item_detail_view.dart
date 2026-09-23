import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/widgets/comment_tile.dart';
import 'package:weaver/features/work_item_detail/widgets/date_field.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';

/// Opens [WorkItemDetailView] in a [Dialog] sized to fit comfortably on
/// both desktop and mobile viewports. If [onDrillInto] is given, an app-bar
/// action lets the user close the dialog and re-scope the board to this
/// item's children instead. If [onDeleted] is given, it's called after the
/// user confirms and successfully deletes this work item, once the dialog
/// has already closed — the caller's chance to refresh whatever list was
/// showing it.
Future<void> showWorkItemDetailDialog(
  BuildContext context, {
  required String workItemId,
  VoidCallback? onDrillInto,
  VoidCallback? onDeleted,
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
            onDeleted: onDeleted,
          ),
        ),
      );
    },
  );
}

class WorkItemDetailView extends StatefulWidget {
  const WorkItemDetailView({
    required this.workItemId,
    this.onDrillInto,
    this.onDeleted,
    super.key,
  });

  final String workItemId;

  /// When set, shown as an app-bar action that closes this view and hands
  /// control back to the caller to re-scope the board to this item's
  /// children — the same navigation [BoardView.drillInto] performs, just
  /// reachable from the detail dialog instead of a tap on the card itself.
  final VoidCallback? onDrillInto;

  /// Called once this item has actually been deleted (after confirmation),
  /// after this view's own dialog has closed itself.
  final VoidCallback? onDeleted;

  @override
  State<WorkItemDetailView> createState() => _WorkItemDetailViewState();
}

class _WorkItemDetailViewState extends State<WorkItemDetailView> {
  final WorkItemDetailViewModel _viewModel = getIt<WorkItemDetailViewModel>();
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _newCommentController = TextEditingController();
  final _linkTargetController = TextEditingController();
  final _tagInputController = TextEditingController();
  bool _fieldsInitialized = false;
  String? _selectedLayerId;
  WorkItemPriority _selectedPriority = WorkItemPriority.medium;
  String? _selectedAssigneeId;
  List<String> _tags = [];

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
      _tags = [...item.tags];
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
    _tagInputController.dispose();
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
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Text(loadError),
              ),
            );
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
                  DropdownMenuItem(
                    value: priority,
                    child: Text(priority.label),
                  ),
              ],
              onChanged: (value) => setState(
                () => _selectedPriority = value ?? WorkItemPriority.medium,
              ),
            ),
            const SizedBox(height: 16),
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
            _buildTagsSection(context),
            const SizedBox(height: 16),
            DateField(
              label: 'Start Date',
              value: item.startDate,
              onPick: () => unawaited(_pickStartDate(item)),
              onClear: item.startDate == null
                  ? null
                  : () =>
                        unawaited(_viewModel.saveSchedule(null, item.endDate)),
            ),
            const SizedBox(height: 16),
            DateField(
              label: 'End Date',
              value: item.endDate,
              onPick: () => unawaited(_pickEndDate(item)),
              onClear: item.endDate == null
                  ? null
                  : () => unawaited(
                      _viewModel.saveSchedule(item.startDate, null),
                    ),
            ),
            const SizedBox(height: 16),
            Text(
              'Created ${formatDate(item.createdAtUtc)} · Updated ${formatDate(item.updatedAtUtc)}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const Divider(height: 32),
            _buildLinksSection(context),
            const Divider(height: 32),
            _buildCommentsSection(context),
            const Divider(height: 32),
            if (_viewModel.saveError != null) ...[
              Text(
                _viewModel.saveError!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
              const SizedBox(height: 8),
            ],
            Row(
              children: [
                OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Theme.of(context).colorScheme.error,
                  ),
                  onPressed: _viewModel.isSaving
                      ? null
                      : () => unawaited(_confirmAndDelete()),
                  child: const Text('Delete'),
                ),
                const Spacer(),
                FilledButton(
                  onPressed: _viewModel.isSaving
                      ? null
                      : () => unawaited(_saveDetails()),
                  child: Text(_viewModel.isSaving ? 'Saving…' : 'Save'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildLinksSection(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Related work items',
          style: Theme.of(context).textTheme.labelLarge,
        ),
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
                decoration: const InputDecoration(
                  hintText: 'Work item id to link',
                ),
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

  Widget _buildTagsSection(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Tags', style: Theme.of(context).textTheme.labelLarge),
        if (_tags.isNotEmpty) ...[
          const SizedBox(height: 4),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              for (final tag in _tags)
                InputChip(
                  label: Text(tag),
                  labelStyle: TextStyle(
                    color: colorScheme.onSecondaryContainer,
                    fontSize: 12,
                  ),
                  backgroundColor: colorScheme.secondaryContainer,
                  visualDensity: VisualDensity.compact,
                  onDeleted: () => unawaited(_removeTag(tag)),
                ),
            ],
          ),
        ],
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _tagInputController,
                decoration: const InputDecoration(hintText: 'Add a tag'),
                onSubmitted: (_) => unawaited(_addTag()),
              ),
            ),
            const SizedBox(width: 8),
            TextButton(
              onPressed: () => unawaited(_addTag()),
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
        for (final comment in _viewModel.comments)
          CommentTile(
            comment: comment,
            isOwnComment: comment.authorUserId == _currentUserId,
            onDelete: () => unawaited(_viewModel.deleteComment(comment.id)),
            onEdit: (body) =>
                unawaited(_viewModel.updateComment(comment.id, body)),
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

  Future<void> _saveDetails() async {
    await _viewModel.saveDetails(
      title: _titleController.text,
      description: _descriptionController.text.isEmpty
          ? null
          : _descriptionController.text,
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

  Future<void> _addTag() async {
    final tag = _tagInputController.text.trim();
    if (tag.isEmpty) return;
    if (_tags.any((existing) => existing.toLowerCase() == tag.toLowerCase())) {
      _tagInputController.clear();
      return;
    }
    await _saveTags([..._tags, tag]);
    _tagInputController.clear();
  }

  Future<void> _removeTag(String tag) => _saveTags([
    for (final existing in _tags)
      if (existing != tag) existing,
  ]);

  Future<void> _saveTags(List<String> updated) async {
    final previous = _tags;
    setState(() => _tags = updated);
    final ok = await _viewModel.saveTags(updated);
    if (!ok && mounted) setState(() => _tags = previous);
  }

  Future<void> _pickStartDate(WorkItemDetail item) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: item.startDate ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked == null) return;
    await _viewModel.saveSchedule(picked, item.endDate);
  }

  Future<void> _pickEndDate(WorkItemDetail item) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: item.endDate ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked == null) return;
    await _viewModel.saveSchedule(item.startDate, picked);
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

  Future<void> _confirmAndDelete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete work item?'),
        content: const Text(
          'This will permanently delete this work item and any sub-items. This cannot be undone.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(dialogContext).colorScheme.error,
            ),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    final ok = await _viewModel.deleteItem();
    if (ok && mounted) {
      Navigator.of(context).pop();
      widget.onDeleted?.call();
    }
  }
}
