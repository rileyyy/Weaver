import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/work_item_detail/models/space_move_mapping.dart';
import 'package:weaver/features/work_item_detail/models/space_move_plan.dart';
import 'package:weaver/features/work_item_detail/space_move_view_model.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/work_item_picker/work_item_picker_dialog.dart';

/// Asks where to move [item] and its sub-items, showing how each status,
/// Type and priority maps onto the chosen Space before the move is
/// confirmed. Completes with true once the item has moved.
Future<bool> showMoveToSpaceDialog(
  BuildContext context, {
  required WorkItem item,
  SpaceMoveViewModel? viewModel,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => MoveToSpaceDialog(
        item: item,
        viewModel: viewModel ?? getIt<SpaceMoveViewModel>(),
      ),
    ) ??
    false;

class MoveToSpaceDialog extends StatefulWidget {
  const MoveToSpaceDialog({
    required this.item,
    required this.viewModel,
    super.key,
  });

  final WorkItem item;

  /// Disposed with the dialog.
  final SpaceMoveViewModel viewModel;

  @override
  State<MoveToSpaceDialog> createState() => _MoveToSpaceDialogState();
}

class _MoveToSpaceDialogState extends State<MoveToSpaceDialog> {
  late final SpaceMoveViewModel _viewModel = widget.viewModel;

  @override
  void initState() {
    super.initState();
    _viewModel.addListener(_onChanged);
    unawaited(_viewModel.start(widget.item));
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_onChanged)
      ..dispose();
    super.dispose();
  }

  void _onChanged() => setState(() {});

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Move to another Space'),
    content: SizedBox(
      width: 480,
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            _spacePicker(),
            if (_viewModel.isLoading) const LinearProgressIndicator(),
            ..._planSection(context),
            if (_viewModel.error case final error?)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  error,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
          ],
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.of(context).pop(false),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: _viewModel.canMove ? () => unawaited(_move()) : null,
        child: Text(_viewModel.isMoving ? 'Moving…' : 'Move'),
      ),
    ],
  );

  Widget _spacePicker() => DropdownButtonFormField<String>(
    key: const ValueKey('move-target-space'),
    initialValue: _viewModel.targetSpaceId,
    decoration: const InputDecoration(labelText: 'Space'),
    items: [
      for (final space in _viewModel.spaces)
        DropdownMenuItem(value: space.id, child: Text(space.name)),
    ],
    onChanged: (spaceId) {
      if (spaceId != null) unawaited(_viewModel.selectSpace(spaceId));
    },
  );

  List<Widget> _planSection(BuildContext context) {
    final plan = _viewModel.plan;
    if (plan == null) return const [];
    final textTheme = Theme.of(context).textTheme;
    return [
      const SizedBox(height: 12),
      Text(_summary(plan)),
      _parentRow(),
      if (plan.statuses.isNotEmpty) ...[
        Text('Statuses', style: textTheme.titleSmall),
        for (final mapping in plan.statuses)
          _mappingRow(
            mapping,
            value: _viewModel.statusChoice(mapping.sourceId),
            options: {for (final s in _viewModel.targetStatuses) s.id: s.name},
            onChanged: (id) {
              if (id != null) _viewModel.chooseStatus(mapping.sourceId, id);
            },
          ),
      ],
      if (plan.types.isNotEmpty) ...[
        Text('Types', style: textTheme.titleSmall),
        for (final mapping in plan.types)
          _mappingRow(
            mapping,
            value: _viewModel.typeChoice(mapping.sourceId),
            options: {for (final t in _viewModel.targetTypes) t.id: t.name},
            noneLabel: 'No Type',
            onChanged: (id) => _viewModel.chooseType(mapping.sourceId, id),
          ),
      ],
      if (plan.priorities.isNotEmpty) ...[
        Text('Priorities', style: textTheme.titleSmall),
        for (final mapping in plan.priorities)
          _mappingRow(
            mapping,
            value: _viewModel.priorityChoice(mapping.sourceId),
            options: {
              for (final p in _viewModel.targetPriorities) p.id: p.name,
            },
            noneLabel: 'No priority',
            onChanged: (id) => _viewModel.choosePriority(mapping.sourceId, id),
          ),
      ],
    ];
  }

  /// Sub-items in the Trash move too, so a restore keeps them with their
  /// parent; saying so explains values in the mapping nobody can see.
  static String _summary(SpaceMovePlan plan) {
    final subItems = plan.itemCount - 1;
    final trashed = plan.trashedItemCount == 0
        ? ''
        : ' (${plan.trashedItemCount} of them in the Trash)';
    final items = subItems == 0
        ? 'This work item moves.'
        : 'This work item and $subItems '
              '${subItems == 1 ? 'sub-item' : 'sub-items'}$trashed move.';
    final clearedValues = plan.clearedFieldValueCount;
    if (clearedValues == 0) return items;
    final values = clearedValues == 1 ? 'value is' : 'values are';
    return '$items $clearedValues custom field $values cleared, since each '
        "Space's Types have their own fields; History keeps them.";
  }

  Widget _parentRow() {
    final parent = _viewModel.parent;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: const Text('Parent'),
      subtitle: Text(
        parent == null
            ? 'None (top level)'
            : '#${parent.number} ${parent.title}',
      ),
      trailing: TextButton(
        onPressed: () => unawaited(_pickParent()),
        child: const Text('Change'),
      ),
    );
  }

  /// [noneLabel] offers clearing the value, for Types and priorities.
  Widget _mappingRow(
    SpaceMoveMapping mapping, {
    required String? value,
    required Map<String, String> options,
    required ValueChanged<String?> onChanged,
    String? noneLabel,
  }) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      children: [
        Expanded(
          child: Text(
            '${mapping.sourceName} (${mapping.itemCount})',
            overflow: TextOverflow.ellipsis,
          ),
        ),
        const Icon(Icons.arrow_forward, size: 16),
        const SizedBox(width: 8),
        Expanded(
          child: DropdownButton<String?>(
            key: ValueKey('move-map-${mapping.sourceId}'),
            isExpanded: true,
            value: value,
            items: [
              if (noneLabel != null)
                DropdownMenuItem<String?>(child: Text(noneLabel)),
              for (final MapEntry(key: id, value: name) in options.entries)
                DropdownMenuItem<String?>(value: id, child: Text(name)),
            ],
            onChanged: onChanged,
          ),
        ),
      ],
    ),
  );

  Future<void> _pickParent() async {
    final pick = await showWorkItemPicker(
      context,
      title: 'Choose a parent',
      search: _viewModel.searchParents,
      noneLabel: 'No parent',
    );
    if (pick != null) _viewModel.chooseParent(pick.item);
  }

  Future<void> _move() async {
    final moved = await _viewModel.move();
    if (moved && mounted) Navigator.of(context).pop(true);
  }
}
