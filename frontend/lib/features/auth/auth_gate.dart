import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/auth/auth_view_model.dart';
import 'package:weaver/features/auth/login_view.dart';
import 'package:weaver/features/board/board_view.dart';

/// The app's root: shows [LoginView] or [BoardView] depending on
/// [AuthViewModel.isAuthenticated], reacting live rather than deciding once
/// at startup — a session cleared mid-use (e.g. a revoked/expired refresh
/// token; see `AuthHttpClient`) bounces back to the login screen on its own.
class AuthGate extends StatefulWidget {
  const AuthGate({super.key});

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

        return BoardView(onLogout: _viewModel.logout);
      },
    );
  }
}
