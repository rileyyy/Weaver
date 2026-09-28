import 'package:flutter/material.dart';

/// Password and confirmation, for setting someone else's password where a
/// typo would lock them out.
class PasswordFields extends StatelessWidget {
  const PasswordFields({
    super.key,
    required this.password,
    required this.confirmation,
    required this.onChanged,
    this.enabled = true,
  });

  final TextEditingController password;
  final TextEditingController confirmation;
  final VoidCallback onChanged;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: password,
          enabled: enabled,
          obscureText: true,
          decoration: const InputDecoration(
            labelText: 'Password',
            helperText: 'At least 12 characters.',
          ),
          onChanged: (_) => onChanged(),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: confirmation,
          enabled: enabled,
          obscureText: true,
          decoration: const InputDecoration(labelText: 'Confirm password'),
          onChanged: (_) => onChanged(),
        ),
      ],
    );
  }
}

/// Both fields filled in and matching. Length is the server's rule, so its
/// message is shown instead of a second copy here.
bool passwordsReady(
  TextEditingController password,
  TextEditingController confirmation,
) => password.text.isNotEmpty && password.text == confirmation.text;

/// Shown once the confirmation has been typed and differs.
String? passwordMismatchError(
  TextEditingController password,
  TextEditingController confirmation,
) => confirmation.text.isNotEmpty && password.text != confirmation.text
    ? 'The passwords don’t match.'
    : null;
