import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';
import 'package:weaver/shared/recurrence/widgets/recurrence_editor.dart';

/// Edits one repeating item's schedule. [onSave] returns null on success
/// or an error to show, keeping the dialog open. Completes with whether a
/// save succeeded.
Future<bool> showEditRecurrenceDialog(
  BuildContext context, {
  required WorkItemRecurrence recurrence,
  required Future<String?> Function(RecurrenceDraft draft) onSave,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) =>
          _EditRecurrenceDialog(recurrence: recurrence, onSave: onSave),
    ) ??
    false;

class _EditRecurrenceDialog extends StatefulWidget {
  const _EditRecurrenceDialog({required this.recurrence, required this.onSave});

  final WorkItemRecurrence recurrence;
  final Future<String?> Function(RecurrenceDraft draft) onSave;

  @override
  State<_EditRecurrenceDialog> createState() => _EditRecurrenceDialogState();
}

class _EditRecurrenceDialogState extends State<_EditRecurrenceDialog> {
  late RecurrenceDraft _draft = widget.recurrence.toDraft();
  bool _isSaving = false;
  String? _error;

  bool get _canSave =>
      !_isSaving &&
      _draft.validationError == null &&
      !_draft.isEquivalentTo(widget.recurrence.toDraft());

  Future<void> _save() async {
    setState(() {
      _isSaving = true;
      _error = null;
    });
    final error = await widget.onSave(_draft);
    if (!mounted) return;
    if (error == null) {
      Navigator.of(context).pop(true);
      return;
    }
    setState(() {
      _isSaving = false;
      _error = error;
    });
  }

  @override
  Widget build(BuildContext context) {
    final recurrence = widget.recurrence;
    final error = _error ?? _draft.validationError;
    return AlertDialog(
      title: Text(
        '#${recurrence.workItemNumber} ${recurrence.workItemTitle}',
        overflow: TextOverflow.ellipsis,
      ),
      content: SizedBox(
        width: 380,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              RecurrenceEditor(
                draft: _draft,
                enabled: !_isSaving,
                onChanged: (draft) => setState(() => _draft = draft),
              ),
              if (error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(
                    error,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _isSaving ? null : () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _canSave ? () => unawaited(_save()) : null,
          child: Text(_isSaving ? 'Saving…' : 'Save'),
        ),
      ],
    );
  }
}
