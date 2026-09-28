import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';

/// One value a deleted lookup's work items can move to.
typedef ReplacementOption = ({String id, String name});

/// Confirms deleting [name] and asks where its work items should go.
/// [noneLabel] offers "no value" as well (priorities and layers); without
/// it a replacement must be picked (statuses). [onDelete] gets the chosen
/// id, or null for none, and returns null on success or an error to show.
/// Completes with whether the delete succeeded.
Future<bool> showDeleteLookupDialog(
  BuildContext context, {
  required String kind,
  required String name,
  required List<ReplacementOption> replacements,
  required Future<String?> Function(String? replacementId) onDelete,
  String? noneLabel,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _DeleteLookupDialog(
        kind: kind,
        name: name,
        replacements: replacements,
        noneLabel: noneLabel,
        onDelete: onDelete,
      ),
    ) ??
    false;

class _DeleteLookupDialog extends StatefulWidget {
  const _DeleteLookupDialog({
    required this.kind,
    required this.name,
    required this.replacements,
    required this.noneLabel,
    required this.onDelete,
  });

  final String kind;
  final String name;
  final List<ReplacementOption> replacements;
  final String? noneLabel;
  final Future<String?> Function(String? replacementId) onDelete;

  @override
  State<_DeleteLookupDialog> createState() => _DeleteLookupDialogState();
}

class _DeleteLookupDialogState extends State<_DeleteLookupDialog>
    with DialogSubmission<_DeleteLookupDialog> {
  late String? _replacementId = widget.noneLabel != null
      ? null
      : widget.replacements.firstOrNull?.id;

  bool get _canDelete => widget.noneLabel != null || _replacementId != null;

  void _submit() {
    if (!_canDelete) return;
    unawaited(runSubmission(() => widget.onDelete(_replacementId)));
  }

  @override
  Widget build(BuildContext context) {
    final noneLabel = widget.noneLabel;
    return AdminFormDialog(
      title: 'Delete ${widget.kind}?',
      submitLabel: 'Delete',
      isDestructive: true,
      isSaving: isSaving,
      error: error,
      onSubmit: _canDelete ? _submit : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Work items with the ${widget.kind} "${widget.name}" will be '
            'changed to:',
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String?>(
            initialValue: _replacementId,
            decoration: const InputDecoration(labelText: 'Replacement'),
            items: [
              if (noneLabel != null)
                DropdownMenuItem(value: null, child: Text(noneLabel)),
              for (final option in widget.replacements)
                DropdownMenuItem(value: option.id, child: Text(option.name)),
            ],
            onChanged: isSaving
                ? null
                : (id) => setState(() => _replacementId = id),
          ),
        ],
      ),
    );
  }
}
