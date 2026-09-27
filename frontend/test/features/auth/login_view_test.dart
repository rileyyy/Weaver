import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/auth_view_model.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/login_view.dart';

import 'fakes/fake_auth_repository.dart';
import 'fakes/in_memory_token_store.dart';

void main() {
  late FakeAuthRepository repository;
  late AuthViewModel viewModel;

  Future<void> pumpLogin(
    WidgetTester tester, {
    FakeAuthRepository? withRepository,
  }) async {
    repository = withRepository ?? FakeAuthRepository();
    viewModel = AuthViewModel(
      repository,
      AuthSessionStore(repository, InMemoryTokenStore()),
    );
    addTearDown(viewModel.dispose);
    await tester.pumpWidget(MaterialApp(home: LoginView(viewModel: viewModel)));
  }

  Future<void> enterCredentials(WidgetTester tester, String username) async {
    await tester.enterText(
      find.widgetWithText(TextField, 'Username'),
      username,
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Password'),
      'correct horse battery',
    );
  }

  testWidgets('signs in with the trimmed username', (tester) async {
    await pumpLogin(tester);

    await enterCredentials(tester, '  alice ');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();

    expect(repository.loginCalls, ['alice']);
    expect(viewModel.isAuthenticated, isTrue);
  });

  testWidgets('does nothing while a field is empty', (tester) async {
    await pumpLogin(tester);

    await tester.enterText(find.widgetWithText(TextField, 'Username'), 'alice');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();

    expect(repository.loginCalls, isEmpty);
  });

  testWidgets('shows the error when sign-in fails', (tester) async {
    await pumpLogin(
      tester,
      withRepository: FakeAuthRepository(
        loginError: const ApiException('bad', statusCode: 401),
      ),
    );

    await enterCredentials(tester, 'alice');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();

    expect(
      find.text('Could not sign in. Check your username and password.'),
      findsOneWidget,
    );
  });

  testWidgets('disables the form while signing in', (tester) async {
    await pumpLogin(tester);
    repository.loginGate = Completer<void>();

    await enterCredentials(tester, 'alice');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();

    final button = tester.widget<FilledButton>(find.byType(FilledButton));
    expect(button.onPressed, isNull);
    expect(find.text('Please wait…'), findsOneWidget);

    repository.loginGate!.complete();
    await tester.pump();
  });

  testWidgets('the toggle switches the form to registration', (tester) async {
    await pumpLogin(tester);

    await tester.tap(find.text("Don't have an account? Create one"));
    await tester.pump();
    expect(find.text('At least 12 characters'), findsOneWidget);

    await enterCredentials(tester, 'bob');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pump();

    expect(repository.registerCalls, ['bob']);
    expect(repository.loginCalls, isEmpty);
  });
}
