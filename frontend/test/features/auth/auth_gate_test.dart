import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/auth/auth_gate.dart';
import 'package:weaver/features/auth/auth_view_model.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/login_view.dart';

import 'fakes/fake_auth_repository.dart';
import 'fakes/in_memory_token_store.dart';

void main() {
  late AuthSessionStore sessionStore;

  setUp(() async {
    await getIt.reset();
    final repository = FakeAuthRepository();
    sessionStore = AuthSessionStore(repository, InMemoryTokenStore());
    getIt.registerFactory<AuthViewModel>(
      () => AuthViewModel(repository, sessionStore),
    );
  });

  Widget gate() => MaterialApp(
    home: AuthGate(
      signedInBuilder: (onLogout) => Scaffold(
        body: TextButton(onPressed: onLogout, child: const Text('Signed in')),
      ),
    ),
  );

  testWidgets('shows a spinner until the session restore has finished', (
    tester,
  ) async {
    await tester.pumpWidget(gate());

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pump();

    expect(find.byType(LoginView), findsOneWidget);
  });

  testWidgets('shows the signed-in screen for a restored session', (
    tester,
  ) async {
    await sessionStore.setSession(fakeSession('alice'));

    await tester.pumpWidget(gate());
    await tester.pump();

    expect(find.text('Signed in'), findsOneWidget);
  });

  testWidgets('returns to the login screen when the session ends', (
    tester,
  ) async {
    await sessionStore.setSession(fakeSession('alice'));
    await tester.pumpWidget(gate());
    await tester.pump();

    await tester.tap(find.text('Signed in'));
    await tester.pump();

    expect(find.byType(LoginView), findsOneWidget);
  });
}
