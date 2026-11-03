import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:weaver/features/admin/agent_keys_view_model.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';

/// Asks for a key's label and scope, then shows the issued key once with a
/// copy button. Completes when the dialog is closed.
///
/// Not dismissible by tapping outside, so the one chance to copy the key
/// isn't lost to a stray click.
Future<void> showIssueKeyDialog(
  BuildContext context, {
  required String agentName,
  required Future<KeyIssueResult> Function({
    required String label,
    required ApiKeyScope scope,
  })
  onIssue,
}) => showDialog<void>(
  context: context,
  barrierDismissible: false,
  builder: (_) => _IssueKeyDialog(agentName: agentName, onIssue: onIssue),
);

class _IssueKeyDialog extends StatefulWidget {
  const _IssueKeyDialog({required this.agentName, required this.onIssue});

  final String agentName;
  final Future<KeyIssueResult> Function({
    required String label,
    required ApiKeyScope scope,
  })
  onIssue;

  @override
  State<_IssueKeyDialog> createState() => _IssueKeyDialogState();
}

class _IssueKeyDialogState extends State<_IssueKeyDialog> {
  final _label = TextEditingController();
  ApiKeyScope _scope = ApiKeyScope.readOnly;
  bool _isSaving = false;
  String? _error;
  String? _issuedKey;
  bool _isCopied = false;

  @override
  void dispose() {
    _label.dispose();
    super.dispose();
  }

  bool get _canIssue => _label.text.trim().isNotEmpty;

  Future<void> _issue() async {
    setState(() {
      _isSaving = true;
      _error = null;
    });
    final result = await widget.onIssue(label: _label.text, scope: _scope);
    if (!mounted) return;
    setState(() {
      _isSaving = false;
      _error = result.error;
      _issuedKey = result.key;
    });
  }

  Future<void> _copy(String key) async {
    await Clipboard.setData(ClipboardData(text: key));
    if (mounted) setState(() => _isCopied = true);
  }

  @override
  Widget build(BuildContext context) {
    final issuedKey = _issuedKey;
    return issuedKey == null ? _buildForm() : _buildReveal(issuedKey);
  }

  Widget _buildForm() {
    return AdminFormDialog(
      title: 'Issue a key for ${widget.agentName}',
      submitLabel: 'Issue',
      isSaving: _isSaving,
      error: _error,
      onSubmit: _canIssue ? () => unawaited(_issue()) : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _label,
            autofocus: true,
            enabled: !_isSaving,
            maxLength: 100,
            decoration: const InputDecoration(
              labelText: 'Label',
              helperText: 'Where the key is used, e.g. “laptop”.',
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 12),
          SegmentedButton<ApiKeyScope>(
            segments: [
              for (final scope in ApiKeyScope.values)
                ButtonSegment(value: scope, label: Text(scope.label)),
            ],
            selected: {_scope},
            onSelectionChanged: _isSaving
                ? null
                : (selection) => setState(() => _scope = selection.single),
          ),
          const SizedBox(height: 8),
          Text(_scope.explanation),
          const SizedBox(height: 4),
          Text(
            'Neither scope can change admin data.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }

  Widget _buildReveal(String key) {
    final colors = Theme.of(context).colorScheme;
    return AlertDialog(
      title: const Text('Copy the new key'),
      content: SizedBox(
        width: 380,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Copy this key now and store it somewhere safe. It won’t be '
              'shown again.',
              style: TextStyle(color: colors.error),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: SelectableText(
                    key,
                    style: const TextStyle(fontFamily: 'monospace'),
                  ),
                ),
                IconButton(
                  tooltip: 'Copy key',
                  icon: Icon(_isCopied ? Icons.check : Icons.copy),
                  onPressed: () => unawaited(_copy(key)),
                ),
              ],
            ),
            if (_isCopied) const Text('Copied'),
          ],
        ),
      ),
      actions: [
        FilledButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Done'),
        ),
      ],
    );
  }
}
