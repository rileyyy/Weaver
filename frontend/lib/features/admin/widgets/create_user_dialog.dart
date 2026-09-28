import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/new_user.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/features/admin/widgets/password_fields.dart';

/// Adds an account with a starting password to pass on to its owner.
/// [onCreate] returns null on success or an error to show, keeping the
/// dialog open. Completes with whether the account was created.
Future<bool> showCreateUserDialog(
  BuildContext context, {
  required Future<String?> Function(NewUser user) onCreate,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _CreateUserDialog(onCreate: onCreate),
    ) ??
    false;

class _CreateUserDialog extends StatefulWidget {
  const _CreateUserDialog({required this.onCreate});

  final Future<String?> Function(NewUser user) onCreate;

  @override
  State<_CreateUserDialog> createState() => _CreateUserDialogState();
}

class _CreateUserDialogState extends State<_CreateUserDialog>
    with DialogSubmission<_CreateUserDialog> {
  final _username = TextEditingController();
  final _password = TextEditingController();
  final _confirmation = TextEditingController();
  bool _isAdmin = false;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    _confirmation.dispose();
    super.dispose();
  }

  bool get _canCreate =>
      _username.text.trim().isNotEmpty &&
      passwordsReady(_password, _confirmation);

  void _submit() {
    if (!_canCreate) return;
    final user = NewUser(
      username: _username.text.trim(),
      password: _password.text,
      isAdmin: _isAdmin,
    );
    unawaited(runSubmission(() => widget.onCreate(user)));
  }

  @override
  Widget build(BuildContext context) {
    return AdminFormDialog(
      title: 'Add user',
      submitLabel: 'Add',
      isSaving: isSaving,
      error: error ?? passwordMismatchError(_password, _confirmation),
      onSubmit: _canCreate ? _submit : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _username,
            autofocus: true,
            enabled: !isSaving,
            decoration: const InputDecoration(labelText: 'Username'),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 12),
          PasswordFields(
            password: _password,
            confirmation: _confirmation,
            enabled: !isSaving,
            onChanged: () => setState(() {}),
          ),
          const SizedBox(height: 8),
          CheckboxListTile(
            contentPadding: EdgeInsets.zero,
            value: _isAdmin,
            title: const Text('Administrator'),
            subtitle: const Text('Can open this Admin page.'),
            onChanged: isSaving
                ? null
                : (value) => setState(() => _isAdmin = value ?? false),
          ),
        ],
      ),
    );
  }
}
