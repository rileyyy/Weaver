import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/auth/auth_view_model.dart';
import 'package:weaver/features/auth/login_view.dart';

/// The app's root: shows [LoginView] or [BoardView] depending on
/// [AuthViewModel.isAuthenticated], reacting live rather than deciding once
/// at startup — a session cleared mid-use (e.g. a revoked/expired refresh
/// token; see `AuthHttpClient`) bounces back to the login screen on its own.
class AuthGate extends StatefulWidget {
  const AuthGate({required this.signedInBuilder, super.key});

  /// The app's signed-in screen, given the sign-out action. Supplied by the
  /// composition root so the auth feature doesn't import other features.
  final Widget Function(Future<void> Function() onLogout) signedInBuilder;

  @override
  State<AuthGate> createState() => _AuthGateState();
}

class _AuthGateState extends State<AuthGate> {
  final AuthViewModel _viewModel = getIt<AuthViewModel>();

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.restoreSession());
  }

  @override
  void dispose() {
    _viewModel.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: _viewModel,
      builder: (context, _) {
        if (!_viewModel.hasRestored) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }

        if (!_viewModel.isAuthenticated) {
          return LoginView(viewModel: _viewModel);
        }

        return widget.signedInBuilder(_viewModel.logout);
      },
    );
  }
}
