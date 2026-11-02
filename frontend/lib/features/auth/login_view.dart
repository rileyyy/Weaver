import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/auth/auth_view_model.dart';

/// Username/password sign-in. There is no registration: an admin creates
/// every account (ADR-0004). Native builds also ask for the server's
/// address here.
class LoginView extends StatefulWidget {
  const LoginView({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  State<LoginView> createState() => _LoginViewState();
}

class _LoginViewState extends State<LoginView> {
  late final _serverController = TextEditingController(
    text: widget.viewModel.serverOrigin,
  );
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _serverController.dispose();
    _usernameController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final username = _usernameController.text.trim();
    final password = _passwordController.text;
    if (username.isEmpty || password.isEmpty) return;

    final serverAddress = _serverController.text;
    await widget.viewModel.login(
      username,
      password,
      serverAddress: serverAddress,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        // Scrolls so the on-screen keyboard can't push the form off a phone.
        child: SingleChildScrollView(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: ListenableBuilder(
                listenable: widget.viewModel,
                builder: (context, _) {
                  final viewModel = widget.viewModel;
                  return Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'Weaver',
                        style: Theme.of(context).textTheme.headlineMedium,
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 24),
                      if (viewModel.canChangeServer) ...[
                        TextField(
                          controller: _serverController,
                          decoration: const InputDecoration(
                            labelText: 'Server',
                            hintText: 'https://weaver.example.ts.net',
                          ),
                          keyboardType: TextInputType.url,
                          autocorrect: false,
                          enableSuggestions: false,
                          autofillHints: const [AutofillHints.url],
                        ),
                        const SizedBox(height: 12),
                      ],
                      TextField(
                        controller: _usernameController,
                        decoration: const InputDecoration(
                          labelText: 'Username',
                        ),
                        autofillHints: const [AutofillHints.username],
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _passwordController,
                        decoration: const InputDecoration(
                          labelText: 'Password',
                        ),
                        obscureText: true,
                        autofillHints: const [AutofillHints.password],
                        onSubmitted: (_) => unawaited(_submit()),
                      ),
                      const SizedBox(height: 16),
                      if (viewModel.errorMessage != null) ...[
                        Text(
                          viewModel.errorMessage!,
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 12),
                      ],
                      FilledButton(
                        onPressed: viewModel.isSubmitting
                            ? null
                            : () => unawaited(_submit()),
                        child: Text(
                          viewModel.isSubmitting ? 'Please wait…' : 'Sign in',
                        ),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        'Ask an admin for an account.',
                        style: Theme.of(context).textTheme.bodySmall,
                        textAlign: TextAlign.center,
                      ),
                    ],
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}
