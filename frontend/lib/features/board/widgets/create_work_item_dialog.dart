import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/swimlane.dart';

class CreateWorkItemResult {
  const CreateWorkItemResult({
    required this.title,
    this.description,
    required this.parentId,
  });

  final String title;
  final String? description;

  /// Null makes the new item a top-level item with no parent at all.
  final String? parentId;
}

/// Prompts for a new work item's title/description and where it belongs:
/// either a new swimlane under [scopeParentId], or a card inside one of
/// [swimlanes]. Returns null if cancelled.
Future<CreateWorkItemResult?> showCreateWorkItemDialog(
  BuildContext context, {
  required String? scopeParentId,
  required List<Swimlane> swimlanes,
}) {
  return showDialog<CreateWorkItemResult>(
    context: context,
    builder: (context) => _CreateWorkItemDialog(
      scopeParentId: scopeParentId,
      swimlanes: swimlanes,
    ),
  );
}

class _CreateWorkItemDialog extends StatefulWidget {
  const _CreateWorkItemDialog({
    required this.scopeParentId,
    required this.swimlanes,
  });

  final String? scopeParentId;
  final List<Swimlane> swimlanes;

  @override
  State<_CreateWorkItemDialog> createState() => _CreateWorkItemDialogState();
}

class _CreateWorkItemDialogState extends State<_CreateWorkItemDialog> {
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  late bool _asNewSwimlane = widget.swimlanes.isEmpty;
  late String? _selectedSwimlaneParentId =
      widget.swimlanes.isEmpty ? null : widget.swimlanes.first.parentId;

  @override
  void dispose() {
    _titleController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  bool get _canCreate =>
      _titleController.text.trim().isNotEmpty &&
      (_asNewSwimlane || _selectedSwimlaneParentId != null);

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Add Work Item'),
      content: SizedBox(
        width: 360,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _titleController,
              autofocus: true,
              decoration: const InputDecoration(labelText: 'Title'),
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _descriptionController,
              decoration: const InputDecoration(labelText: 'Description'),
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            RadioGroup<bool>(
              groupValue: _asNewSwimlane,
              onChanged: (value) => setState(() => _asNewSwimlane = value ?? true),
              child: Column(
                children: [
                  const RadioListTile<bool>(
                    contentPadding: EdgeInsets.zero,
                    title: Text('New swimlane at this level'),
                    value: true,
                  ),
                  RadioListTile<bool>(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Card in an existing swimlane'),
                    value: false,
                    enabled: widget.swimlanes.isNotEmpty,
                  ),
                ],
              ),
            ),
            if (!_asNewSwimlane)
              Padding(
                padding: const EdgeInsets.only(left: 16),
                child: DropdownButtonFormField<String>(
                  initialValue: _selectedSwimlaneParentId,
                  decoration: const InputDecoration(labelText: 'Swimlane'),
                  items: [
                    for (final lane in widget.swimlanes)
                      DropdownMenuItem(value: lane.parentId, child: Text(lane.title)),
                  ],
                  onChanged: (value) => setState(() => _selectedSwimlaneParentId = value),
                ),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _canCreate
              ? () => Navigator.of(context).pop(
                    CreateWorkItemResult(
                      title: _titleController.text.trim(),
                      description: _descriptionController.text.trim().isEmpty
                          ? null
                          : _descriptionController.text.trim(),
                      parentId:
                          _asNewSwimlane ? widget.scopeParentId : _selectedSwimlaneParentId,
                    ),
                  )
              : null,
          child: const Text('Create'),
        ),
      ],
    );
  }
}
