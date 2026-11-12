import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_relation.dart';
import 'package:weaver/features/work_item_detail/repeat_settings_view_model.dart';
import 'package:weaver/features/work_item_detail/widgets/attachments_section.dart';
import 'package:weaver/features/work_item_detail/widgets/comments_section.dart';
import 'package:weaver/features/work_item_detail/widgets/confirm_dialogs.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_fields_section.dart';
import 'package:weaver/features/work_item_detail/widgets/description_field.dart';
import 'package:weaver/features/work_item_detail/widgets/history_tab.dart';
import 'package:weaver/features/work_item_detail/widgets/links_section.dart';
import 'package:weaver/features/work_item_detail/widgets/move_to_space_dialog.dart';
import 'package:weaver/features/work_item_detail/widgets/parent_field.dart';
import 'package:weaver/features/work_item_detail/widgets/repeat_section.dart';
import 'package:weaver/features/work_item_detail/widgets/roll_ups_section.dart';
import 'package:weaver/features/work_item_detail/widgets/schedule_fields.dart';
import 'package:weaver/features/work_item_detail/widgets/sub_items_section.dart';
import 'package:weaver/features/work_item_detail/widgets/tags_editor.dart';
import 'package:weaver/features/work_item_detail/widgets/url_links_section.dart';
import 'package:weaver/features/work_item_detail/widgets/when_field.dart';
import 'package:weaver/features/work_item_detail/work_item_attachments_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_history_view_model.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/recurrence/widgets/confirm_stop_repeating.dart';
import 'package:weaver/shared/widgets/field_label.dart';
import 'package:weaver/shared/widgets/user_name_label.dart';
import 'package:weaver/shared/work_item_picker/work_item_picker_dialog.dart';

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
/// Edits and deletes reach other views through the shared `WorkItemStore`.
/// Completes with true only if the dialog (or a sub-item dialog opened
/// inside it) changed something the store can't show, such as repeat
/// settings creating items, so the caller can refresh what it was showing.
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
    this.repeatViewModel,
    this.historyViewModel,
    this.attachmentsViewModel,
    super.key,
  });

  final String workItemId;

  /// When set, shown as an app-bar action that closes this view and hands
  /// control back to the caller to re-scope the board to this item's
  /// children — the same navigation [BoardViewModel.drillInto] performs, just
  /// reachable from the detail dialog instead of a tap on the card itself.
  final VoidCallback? onDrillInto;

  /// Called (possibly more than once) after a change the shared store can't
  /// show by itself — see [WorkItemDetailViewModel.hasChanges].
  final VoidCallback? onChanged;

  /// Normally resolved from DI; tests pass one in.
  final WorkItemDetailViewModel? viewModel;

  /// Normally resolved from DI; tests pass one in.
  final RepeatSettingsViewModel? repeatViewModel;

  /// Normally resolved from DI when the History tab is first shown; tests
  /// pass one in.
  final WorkItemHistoryViewModel? historyViewModel;

  /// Normally resolved from DI; tests pass one in.
  final WorkItemAttachmentsViewModel? attachmentsViewModel;

  @override
  State<WorkItemDetailView> createState() => _WorkItemDetailViewState();
}

class _WorkItemDetailViewState extends State<WorkItemDetailView>
    with SingleTickerProviderStateMixin {
  static const int _historyTabIndex = 1;

  late final WorkItemDetailViewModel _viewModel =
      widget.viewModel ?? getIt<WorkItemDetailViewModel>();
  late final RepeatSettingsViewModel _repeat =
      widget.repeatViewModel ?? getIt<RepeatSettingsViewModel>();
  bool _repeatLoadStarted = false;
  late final WorkItemAttachmentsViewModel _attachments =
      widget.attachmentsViewModel ?? getIt<WorkItemAttachmentsViewModel>();

  late final TabController _tabs = TabController(length: 2, vsync: this)
    ..addListener(_onTabChanged);

  /// Created when the History tab is first shown, so the dialog doesn't
  /// fetch history nobody looks at.
  WorkItemHistoryViewModel? _history;

  // The details form (title, description, type, priority) is edited
  // locally and saved with "Save details"; every other field saves as soon
  // as it changes.
  final TextEditingController _titleController = TextEditingController();
  final TextEditingController _descriptionController = TextEditingController();
  String? _selectedTypeId;
  String? _selectedPriorityId;

  String? _selectedAssigneeId;
  List<String> _tags = [];
  int _seededGeneration = -1;

  @override
  void initState() {
    super.initState();
    _viewModel.addListener(_onViewModelChanged);
    _repeat.addListener(_onRepeatChanged);
    _attachments.addListener(_onAttachmentsChanged);
    _titleController.addListener(_onFormEdited);
    _descriptionController.addListener(_onFormEdited);
    unawaited(_viewModel.load(widget.workItemId));
    unawaited(_attachments.load(widget.workItemId));
  }

  void _onFormEdited() => setState(() {});

  /// Reloads on every visit, so changes saved on the Details tab since
  /// the last one show up.
  void _onTabChanged() {
    if (_tabs.indexIsChanging || _tabs.index != _historyTabIndex) return;
    final history = _history ??= _createHistoryViewModel();
    unawaited(history.load(widget.workItemId));
  }

  WorkItemHistoryViewModel _createHistoryViewModel() {
    final history =
        widget.historyViewModel ?? getIt<WorkItemHistoryViewModel>();
    setState(() {});
    return history;
  }

  void _onViewModelChanged() {
    final item = _viewModel.item;
    if (item != null && _seededGeneration != _viewModel.reloadGeneration) {
      _seedFrom(item);
      _seededGeneration = _viewModel.reloadGeneration;
    }
    if (item != null && !_repeatLoadStarted) {
      _repeatLoadStarted = true;
      unawaited(
        _repeat.load(
          workItemId: item.id,
          recurrenceSourceId: item.recurrenceSourceId,
        ),
      );
    }
    if (_viewModel.hasChanges) widget.onChanged?.call();
    setState(() {});
  }

  void _onRepeatChanged() {
    if (_repeat.hasChanges) widget.onChanged?.call();
    setState(() {});
  }

  void _onAttachmentsChanged() => setState(() {});

  void _seedFrom(WorkItem item) {
    _titleController.text = item.title;
    _descriptionController.text = item.description ?? '';
    _selectedTypeId = item.typeId;
    _selectedPriorityId = item.priorityId;
    _selectedAssigneeId = item.assignedToUserId;
    _tags = [...item.tags];
  }

  /// Whether the details form differs from the last saved item.
  bool get _hasUnsavedDetails {
    final item = _viewModel.item;
    if (item == null) return false;
    return _titleController.text != item.title ||
        _descriptionController.text != (item.description ?? '') ||
        _selectedTypeId != item.typeId ||
        _selectedPriorityId != item.priorityId;
  }

  bool get _hasUnsavedChanges => _hasUnsavedDetails || _repeat.isDirty;

  // A tick is applied to the saved description, so the boxes would no
  // longer match what's shown once it has unsaved edits; and while a save
  // is in flight the next tick would carry a stale version.
  bool get _canTickTasks =>
      !_viewModel.isSaving &&
      _descriptionController.text == (_viewModel.item?.description ?? '');

  @override
  void dispose() {
    _viewModel
      ..removeListener(_onViewModelChanged)
      ..dispose();
    _repeat
      ..removeListener(_onRepeatChanged)
      ..dispose();
    _attachments
      ..removeListener(_onAttachmentsChanged)
      ..dispose();
    _history?.dispose();
    _tabs.dispose();
    _titleController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final onDrillInto = widget.onDrillInto;
    return PopScope(
      // Tapping outside the dialog or pressing back mustn't silently drop
      // an unsaved title, description or repeat schedule.
      canPop: !_hasUnsavedChanges,
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
          bottom: TabBar(
            controller: _tabs,
            tabs: const [
              Tab(text: 'Details'),
              Tab(text: 'History'),
            ],
          ),
        ),
        body: TabBarView(
          controller: _tabs,
          children: [
            _buildBody(context),
            switch (_history) {
              final history? => HistoryTab(viewModel: history),
              null => const SizedBox.shrink(),
            },
          ],
        ),
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

    // Null once the item is deleted, while the dialog animates closed.
    final item = _viewModel.item;
    if (item == null) return const SizedBox.shrink();
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
              final primary = _primaryColumn(item);
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
  Widget _primaryColumn(WorkItem item) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        DescriptionField(
          controller: _descriptionController,
          onToggleTask: _canTickTasks
              ? (index) => unawaited(_toggleTask(index))
              : null,
        ),
        const SizedBox(height: 20),
        SubItemsSection(
          children: _viewModel.children,
          statusColorFor: _viewModel.statusColorFor,
          onOpen: (id) => unawaited(_openNestedItem(id)),
          onAdd: _viewModel.addSubItem,
        ),
        const SizedBox(height: 20),
        RepeatSection(
          viewModel: _repeat,
          itemStartDate: item.startDate,
          occurrenceDate: item.recurrenceDate,
          onOpenSource: (id) => unawaited(_openNestedItem(id)),
          onStopRepeating: () => unawaited(_stopRepeating()),
        ),
        const SizedBox(height: 20),
        CommentsSection(
          comments: _viewModel.comments,
          mentionHandles: _viewModel.mentionHandles,
          isOwnComment: _viewModel.isOwnComment,
          onAdd: _viewModel.addComment,
          onEdit: _viewModel.updateComment,
          onDelete: _viewModel.deleteComment,
        ),
      ],
    );
  }

  /// Column 2 (35% width on wide layouts): metadata fields and links.
  Widget _metadataColumn(BuildContext context, WorkItem item) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Type'),
        DropdownButtonFormField<String?>(
          initialValue: _selectedTypeId,
          decoration: const InputDecoration(),
          items: [
            const DropdownMenuItem(value: null, child: Text('None')),
            for (final type in _viewModel.types)
              DropdownMenuItem(value: type.id, child: Text(type.name)),
          ],
          onChanged: (value) => setState(() => _selectedTypeId = value),
        ),
        if (_viewModel.typeChangeClearsFields(_selectedTypeId))
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(
              "Saving another Type clears this item's custom field values.",
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        const SizedBox(height: 16),
        const FieldLabel('Priority'),
        DropdownButtonFormField<String?>(
          initialValue: _selectedPriorityId,
          decoration: const InputDecoration(),
          items: [
            const DropdownMenuItem(value: null, child: Text('None')),
            for (final priority in _viewModel.priorities)
              DropdownMenuItem(value: priority.id, child: Text(priority.name)),
          ],
          onChanged: (value) => setState(() => _selectedPriorityId = value),
        ),
        const SizedBox(height: 16),
        const FieldLabel('Status'),
        // Keyed by the saved status so the picker follows a failed save's
        // revert or a conflict's reload, which `initialValue` alone doesn't.
        KeyedSubtree(
          key: ValueKey(item.statusId),
          child: DropdownButtonFormField<String>(
            key: const Key('status-picker'),
            initialValue: item.statusId,
            decoration: const InputDecoration(),
            items: [
              for (final status in _viewModel.statuses)
                DropdownMenuItem(value: status.id, child: Text(status.name)),
            ],
            onChanged: (value) {
              if (value != null) unawaited(_viewModel.saveStatus(value));
            },
          ),
        ),
        const SizedBox(height: 16),
        const FieldLabel('Parent'),
        ParentField(
          key: const Key('parent-picker'),
          parent: _viewModel.parent,
          onTap: () => unawaited(_pickParent()),
        ),
        const SizedBox(height: 16),
        const FieldLabel('Assigned To'),
        DropdownButtonFormField<String?>(
          initialValue: _selectedAssigneeId,
          decoration: const InputDecoration(),
          items: [
            const DropdownMenuItem(value: null, child: Text('Unassigned')),
            for (final user in _viewModel.users)
              DropdownMenuItem(
                value: user.id,
                child: UserNameLabel(user: user),
              ),
          ],
          onChanged: (value) => unawaited(_saveAssignee(value)),
        ),
        const SizedBox(height: 16),
        TagsEditor(tags: _tags, onChanged: _saveTags),
        const SizedBox(height: 16),
        WhenField(
          when: item.when,
          resolved: item.whenResolved,
          awaitingReResolution: item.whenEditedSinceResolved,
          onSave: _viewModel.saveWhen,
          onQuickPick: _viewModel.applyQuickPick,
        ),
        const SizedBox(height: 16),
        ScheduleFields(
          startDate: item.startDate,
          endDate: item.endDate,
          onChanged: (start, end) =>
              unawaited(_viewModel.saveSchedule(start, end)),
        ),
        const SizedBox(height: 16),
        CustomFieldsSection(
          fields: _viewModel.customFields,
          values: item.fields,
          users: _viewModel.users,
          rollUpFor: _viewModel.rollUpFor,
          onSave: _viewModel.saveCustomField,
        ),
        RollUpsSection(
          rollUps: _viewModel.otherRollUps,
          typeNameFor: _viewModel.typeNameFor,
        ),
        Text(
          [
            'Created ${formatDate(item.createdAt)}',
            'Updated ${formatDate(item.updatedAt)}',
            if (item.completionDate case final completed?)
              'Completed ${formatDate(completed)}',
          ].join(' · '),
          style: Theme.of(context).textTheme.bodySmall,
        ),
        const SizedBox(height: 20),
        LinksSection(
          links: _viewModel.links,
          onAdd: (type) => unawaited(_pickLink(type)),
          onRemove: (id) => unawaited(_viewModel.deleteLink(id)),
          onOpen: (id) => unawaited(_openNestedItem(id)),
        ),
        const SizedBox(height: 20),
        UrlLinksSection(
          links: _viewModel.urlLinks,
          onAdd: (url, label) => _viewModel.addUrlLink(url, label: label),
          onRemove: (id) => unawaited(_viewModel.deleteUrlLink(id)),
        ),
        const SizedBox(height: 20),
        AttachmentsSection(
          attachments: _attachments.attachments,
          isLoading: _attachments.isLoading,
          loadError: _attachments.loadError,
          isUploading: _attachments.isUploading,
          error: _attachments.error,
          onUpload: () => unawaited(_attachments.upload()),
          onDownload: (attachment) =>
              unawaited(_attachments.download(attachment)),
          onDelete: (attachment) => unawaited(_attachments.delete(attachment)),
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
        const SizedBox(width: 8),
        OutlinedButton(
          onPressed: _viewModel.isSaving
              ? null
              : () => unawaited(_moveToSpace()),
          child: const Text('Move to Space…'),
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
    typeId: _selectedTypeId,
    priorityId: _selectedPriorityId,
  );

  Future<void> _toggleTask(int taskIndex) async {
    final ok = await _viewModel.toggleDescriptionTask(taskIndex);
    // A conflict re-seeds the whole form through reloadGeneration; a
    // success only needs the description to follow the saved one.
    if (ok && mounted) {
      _descriptionController.text = _viewModel.item!.description ?? '';
    }
  }

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

  Future<void> _pickParent() async {
    final pick = await showWorkItemPicker(
      context,
      title: 'Choose a parent',
      search: _viewModel.searchParents,
      noneLabel: 'No parent',
    );
    if (pick == null) return;
    await _viewModel.saveParent(pick.item);
  }

  Future<void> _pickLink(WorkItemLinkRelation type) async {
    final pick = await showWorkItemPicker(
      context,
      title: 'Add a "${type.label}" link',
      search: _viewModel.searchLinkTargets,
    );
    final target = pick?.item;
    if (target == null) return;
    await _viewModel.addLink(target, type);
  }

  Future<void> _closeDiscardingChanges() async {
    if (!await confirmDiscardChanges(context) || !mounted) return;
    _seedFrom(_viewModel.item!);
    _repeat.discardChanges();
    Navigator.of(context).pop();
  }

  Future<void> _drillInto(VoidCallback onDrillInto) async {
    if (_hasUnsavedChanges && !await confirmDiscardChanges(context)) return;
    if (!mounted) return;
    Navigator.of(context).pop();
    onDrillInto();
  }

  Future<void> _delete() async {
    if (!await confirmDeleteWorkItem(context)) return;
    final ok = await _viewModel.deleteItem();
    if (ok && mounted) Navigator.of(context).pop();
  }

  /// The move clears the details form's Type and priority on the server, so
  /// unsaved edits there would be saved against the wrong Space.
  Future<void> _moveToSpace() async {
    if (_hasUnsavedChanges && !await confirmDiscardChanges(context)) return;
    if (!mounted) return;
    final moved = await showMoveToSpaceDialog(context, item: _viewModel.item!);
    if (!moved) return;
    _repeat.discardChanges();
    await _viewModel.onMovedToSpace();
  }

  Future<void> _stopRepeating() async {
    if (!await confirmStopRepeating(context)) return;
    await _repeat.stopRepeating();
  }

  /// Opens a sub-item, a linked item, or the item that created this one,
  /// in a dialog on top of this one.
  Future<void> _openNestedItem(String workItemId) async {
    final changed = await showWorkItemDetailDialog(
      context,
      workItemId: workItemId,
    );
    await _viewModel.onNestedDialogClosed(changed: changed);
  }
}
