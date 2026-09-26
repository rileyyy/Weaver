import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/widgets/comments_section.dart';
import 'package:weaver/features/work_item_detail/widgets/confirm_dialogs.dart';
import 'package:weaver/features/work_item_detail/widgets/field_label.dart';
import 'package:weaver/features/work_item_detail/widgets/links_section.dart';
import 'package:weaver/features/work_item_detail/widgets/schedule_fields.dart';
import 'package:weaver/features/work_item_detail/widgets/sub_items_section.dart';
import 'package:weaver/features/work_item_detail/widgets/tags_editor.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';

/// Below this available content width, the two-column layout collapses to a
/// single stacked column — the two-column form is too cramped on a phone
/// (per the project's "board must remain usable on both desktop/web and
/// mobile" rule, which applies to this detail view too).
const double _twoColumnBreakpoint = 640;

/// The 65/35 column split requested for the detail view's content: primary
/// content (description, sub-items, comments) gets the larger share,
/// metadata fields get the rest.
const int _primaryColumnFlex = 65;
const int _metadataColumnFlex = 35;

/// Opens [WorkItemDetailView] in a [Dialog] sized to fit comfortably on
/// both desktop and mobile viewports. If [onDrillInto] is given, an app-bar
/// action lets the user close the dialog and re-scope the board to this
/// item's children instead.
///
/// Completes with true if anything other views show was changed from the
/// dialog (or from a sub-item dialog opened inside it), including deleting
/// the item, so the caller can refresh whatever it was showing.
Future<bool> showWorkItemDetailDialog(
  BuildContext context, {
  required String workItemId,
  VoidCallback? onDrillInto,
}) async {
  var changed = false;
  await showDialog<void>(
    context: context,
    builder: (context) {
      final screenSize = MediaQuery.sizeOf(context);
      return Dialog(
        child: SizedBox(
          width: math.min(960, screenSize.width * 0.95),
          height: math.min(760, screenSize.height * 0.9),
          child: WorkItemDetailView(
            workItemId: workItemId,
            onDrillInto: onDrillInto,
            onChanged: () => changed = true,
          ),
        ),
      );
    },
  );
  return changed;
}

class WorkItemDetailView extends StatefulWidget {
  const WorkItemDetailView({
    required this.workItemId,
    this.onDrillInto,
    this.onChanged,
    this.viewModel,
    super.key,
  });

  final String workItemId;

  /// When set, shown as an app-bar action that closes this view and hands
  /// control back to the caller to re-scope the board to this item's
  /// children — the same navigation [BoardView.drillInto] performs, just
  /// reachable from the detail dialog instead of a tap on the card itself.
  final VoidCallback? onDrillInto;

  /// Called (possibly more than once) after a change other views show has
  /// been saved — see [WorkItemDetailViewModel.hasChanges].
  final VoidCallback? onChanged;

  /// Normally resolved from DI; tests pass one in.
  final WorkItemDetailViewModel? viewModel;

  @override
  State<WorkItemDetailView> createState() => _WorkItemDetailViewState();
}

class _WorkItemDetailViewState extends State<WorkItemDetailView> {
  late final WorkItemDetailViewModel _viewModel =
      widget.viewModel ?? getIt<WorkItemDetailViewModel>();

  // The details form (title, description, layer, priority) is edited
  // locally and saved with "Save details"; every other field saves as soon
  // as it changes.
  final TextEditingController _titleController = TextEditingController();
  final TextEditingController _descriptionController = TextEditingController();
  String? _selectedLayerId;
  WorkItemPriority _selectedPriority = WorkItemPriority.medium;

  String? _selectedAssigneeId;
  List<String> _tags = [];
  int _seededGeneration = -1;

  @override
  void initState() {
    super.initState();
    _viewModel.addListener(_onViewModelChanged);
    _titleController.addListener(_onFormEdited);
    _descriptionController.addListener(_onFormEdited);
    unawaited(_viewModel.load(widget.workItemId));
  }

  void _onFormEdited() => setState(() {});

  void _onViewModelChanged() {
    final item = _viewModel.item;
    if (item != null && _seededGeneration != _viewModel.reloadGeneration) {
      _seedFrom(item);
      _seededGeneration = _viewModel.reloadGeneration;
    }
    if (_viewModel.hasChanges) widget.onChanged?.call();
    setState(() {});
  }

  void _seedFrom(WorkItemDetail item) {
    _titleController.text = item.title;
    _descriptionController.text = item.description ?? '';
    _selectedLayerId = item.layerId;
    _selectedPriority = item.priority;
    _selectedAssigneeId = item.assignedToUserId;
    _tags = [...item.tags];
  }

  /// Whether the details form differs from the last saved item.
  bool get _hasUnsavedDetails {
    final item = _viewModel.item;
    if (item == null) return false;
    return _titleController.text != item.title ||
        _descriptionController.text != (item.description ?? '') ||
        _selectedLayerId != item.layerId ||
        _selectedPriority != item.priority;
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_onViewModelChanged)
      ..dispose();
    _titleController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final onDrillInto = widget.onDrillInto;
    return PopScope(
      // Tapping outside the dialog or pressing back mustn't silently drop
      // an unsaved title or description.
      canPop: !_hasUnsavedDetails,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) unawaited(_closeDiscardingChanges());
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Work Item Details'),
          actions: [
            if (onDrillInto != null)
              IconButton(
                icon: const Icon(Icons.account_tree_outlined),
                tooltip: 'View sub-items',
                onPressed: () => unawaited(_drillInto(onDrillInto)),
              ),
          ],
        ),
        body: _buildBody(context),
      ),
    );
  }

  Widget _buildBody(BuildContext context) {
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

    final item = _viewModel.item!;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            controller: _titleController,
            style: Theme.of(
              context,
            ).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w600),
            decoration: InputDecoration(
              hintText: 'Title',
              errorText: _viewModel.titleError(_titleController.text),
            ),
          ),
          const SizedBox(height: 20),
          LayoutBuilder(
            builder: (context, constraints) {
              final primary = _primaryColumn();
              final metadata = _metadataColumn(context, item);
              if (constraints.maxWidth < _twoColumnBreakpoint) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [primary, const SizedBox(height: 24), metadata],
                );
              }
              return Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(flex: _primaryColumnFlex, child: primary),
                  const SizedBox(width: 32),
                  Expanded(flex: _metadataColumnFlex, child: metadata),
                ],
              );
            },
          ),
          const Divider(height: 32),
          if (_viewModel.saveError case final error?) ...[
            Text(
              error,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
            const SizedBox(height: 8),
          ],
          _actionsRow(context),
        ],
      ),
    );
  }

  /// Column 1 (65% width on wide layouts): the item's main content.
  Widget _primaryColumn() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Description'),
        TextField(
          controller: _descriptionController,
          decoration: const InputDecoration(),
          maxLines: 4,
        ),
        const SizedBox(height: 20),
        SubItemsSection(
          children: _viewModel.children,
          statusColorFor: _viewModel.statusColorFor,
          onOpen: (id) => unawaited(_openSubItem(id)),
        ),
        const SizedBox(height: 20),
        CommentsSection(
          comments: _viewModel.comments,
          isOwnComment: _viewModel.isOwnComment,
          onAdd: _viewModel.addComment,
          onEdit: _viewModel.updateComment,
          onDelete: _viewModel.deleteComment,
        ),
      ],
    );
  }

  /// Column 2 (35% width on wide layouts): metadata fields and links.
  Widget _metadataColumn(BuildContext context, WorkItemDetail item) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Layer'),
        DropdownButtonFormField<String?>(
          initialValue: _selectedLayerId,
          decoration: const InputDecoration(),
          items: [
            const DropdownMenuItem(value: null, child: Text('None')),
            for (final layer in _viewModel.layers)
              DropdownMenuItem(value: layer.id, child: Text(layer.name)),
          ],
          onChanged: (value) => setState(() => _selectedLayerId = value),
        ),
        const SizedBox(height: 16),
        const FieldLabel('Priority'),
        DropdownButtonFormField<WorkItemPriority>(
          initialValue: _selectedPriority,
          decoration: const InputDecoration(),
          items: [
            for (final priority in WorkItemPriority.values)
              DropdownMenuItem(value: priority, child: Text(priority.label)),
          ],
          onChanged: (value) => setState(
            () => _selectedPriority = value ?? WorkItemPriority.medium,
          ),
        ),
        const SizedBox(height: 16),
        const FieldLabel('Status'),
        Text(_viewModel.statusName ?? item.statusId),
        const SizedBox(height: 16),
        const FieldLabel('Assigned To'),
        DropdownButtonFormField<String?>(
          initialValue: _selectedAssigneeId,
          decoration: const InputDecoration(),
          items: [
            const DropdownMenuItem(value: null, child: Text('Unassigned')),
            for (final user in _viewModel.users)
              DropdownMenuItem(value: user.id, child: Text(user.username)),
          ],
          onChanged: (value) => unawaited(_saveAssignee(value)),
        ),
        const SizedBox(height: 16),
        TagsEditor(tags: _tags, onChanged: _saveTags),
        const SizedBox(height: 16),
        ScheduleFields(
          startDate: item.startDate,
          endDate: item.endDate,
          onChanged: (start, end) =>
              unawaited(_viewModel.saveSchedule(start, end)),
        ),
        const SizedBox(height: 16),
        Text(
          'Created ${formatDate(item.createdAt)} · '
          'Updated ${formatDate(item.updatedAt)}',
          style: Theme.of(context).textTheme.bodySmall,
        ),
        const SizedBox(height: 20),
        LinksSection(
          links: _viewModel.links,
          onAdd: _viewModel.addLink,
          onRemove: (id) => unawaited(_viewModel.deleteLink(id)),
        ),
      ],
    );
  }

  Widget _actionsRow(BuildContext context) {
    final canSaveDetails =
        _hasUnsavedDetails &&
        !_viewModel.isSaving &&
        _viewModel.titleError(_titleController.text) == null;
    return Row(
      children: [
        OutlinedButton(
          style: OutlinedButton.styleFrom(
            foregroundColor: Theme.of(context).colorScheme.error,
          ),
          onPressed: _viewModel.isSaving ? null : () => unawaited(_delete()),
          child: const Text('Delete'),
        ),
        const Spacer(),
        if (_hasUnsavedDetails)
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: Text(
              'Unsaved changes',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        FilledButton(
          onPressed: canSaveDetails ? () => unawaited(_saveDetails()) : null,
          child: Text(_viewModel.isSaving ? 'Saving…' : 'Save details'),
        ),
      ],
    );
  }

  Future<void> _saveDetails() => _viewModel.saveDetails(
    title: _titleController.text,
    description: _descriptionController.text.isEmpty
        ? null
        : _descriptionController.text,
    layerId: _selectedLayerId,
    priority: _selectedPriority,
  );

  Future<void> _saveAssignee(String? userId) async {
    final previous = _selectedAssigneeId;
    setState(() => _selectedAssigneeId = userId);
    final ok = await _viewModel.saveAssignee(userId);
    if (!ok && mounted) setState(() => _selectedAssigneeId = previous);
  }

  Future<bool> _saveTags(List<String> updated) async {
    setState(() => _tags = updated);
    final ok = await _viewModel.saveTags(updated);
    // Re-seed from the last server-confirmed item rather than a snapshot
    // taken before this save: another tag save may have succeeded while
    // this one was in flight, and a conflict reload may have replaced it.
    if (!ok && mounted) setState(() => _tags = [..._viewModel.item!.tags]);
    return ok;
  }

  Future<void> _closeDiscardingChanges() async {
    if (!await confirmDiscardChanges(context) || !mounted) return;
    _seedFrom(_viewModel.item!);
    Navigator.of(context).pop();
  }

  Future<void> _drillInto(VoidCallback onDrillInto) async {
    if (_hasUnsavedDetails && !await confirmDiscardChanges(context)) return;
    if (!mounted) return;
    Navigator.of(context).pop();
    onDrillInto();
  }

  Future<void> _delete() async {
    if (!await confirmDeleteWorkItem(context)) return;
    final ok = await _viewModel.deleteItem();
    if (ok && mounted) Navigator.of(context).pop();
  }

  Future<void> _openSubItem(String subItemId) async {
    final changed = await showWorkItemDetailDialog(
      context,
      workItemId: subItemId,
    );
    if (changed) await _viewModel.onSubItemChanged();
  }
}
