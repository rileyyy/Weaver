import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/network_exception.dart';
import 'package:weaver/core/network/server_settings.dart';
import 'package:weaver/features/auth/auth_view_model.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';

import '../../core/network/fakes/in_memory_server_origin_store.dart';
import 'fakes/fake_auth_repository.dart';
import 'fakes/in_memory_token_store.dart';

void main() {
  late FakeAuthRepository repository;
  late AuthSessionStore sessionStore;
  late AuthViewModel viewModel;

  setUp(() {
    repository = FakeAuthRepository();
    sessionStore = AuthSessionStore(repository, InMemoryTokenStore());
    viewModel = AuthViewModel(repository, sessionStore, webServerSettings());
  });

  test('is not authenticated before any login', () {
    expect(viewModel.isAuthenticated, isFalse);
    expect(viewModel.currentUser, isNull);
  });

  test('login on success sets the session and clears any error', () async {
    await viewModel.login('alice', 'a valid password');

    expect(viewModel.isAuthenticated, isTrue);
    expect(viewModel.currentUser!.username, 'alice');
    expect(viewModel.errorMessage, isNull);
    expect(viewModel.isSubmitting, isFalse);
  });

  test(
    'login on failure sets an error message and stays unauthenticated',
    () async {
      viewModel = AuthViewModel(
        FakeAuthRepository(
          loginError: const ApiException('bad creds', statusCode: 401),
        ),
        sessionStore,
        webServerSettings(),
      );

      await viewModel.login('alice', 'wrong password');

      expect(viewModel.isAuthenticated, isFalse);
      expect(viewModel.errorMessage, isNotNull);
    },
  );

  test("a rate-limited login shows the server's wait message", () async {
    const wait = 'Too many sign-in attempts. Wait a minute and try again.';
    viewModel = AuthViewModel(
      FakeAuthRepository(loginError: const ApiException(wait, statusCode: 429)),
      sessionStore,
      webServerSettings(),
    );

    await viewModel.login('alice', 'any password');

    expect(viewModel.errorMessage, wait);
  });

  test('register on success authenticates the same as login', () async {
    await viewModel.register('newuser', 'a valid password');

    expect(viewModel.isAuthenticated, isTrue);
    expect(viewModel.currentUser!.username, 'newuser');
  });

  test('register on failure sets an error message', () async {
    viewModel = AuthViewModel(
      FakeAuthRepository(
        registerError: const ApiException('taken', statusCode: 409),
      ),
      sessionStore,
      webServerSettings(),
    );

    await viewModel.register('alice', 'a valid password');

    expect(viewModel.isAuthenticated, isFalse);
    expect(viewModel.errorMessage, isNotNull);
  });

  test('logout clears the session', () async {
    await viewModel.login('alice', 'a valid password');

    await viewModel.logout();

    expect(viewModel.isAuthenticated, isFalse);
    expect(viewModel.currentUser, isNull);
  });

  test('restoreSession sets hasRestored regardless of outcome', () async {
    expect(viewModel.hasRestored, isFalse);

    await viewModel.restoreSession();

    expect(viewModel.hasRestored, isTrue);
    expect(viewModel.isAuthenticated, isFalse);
  });

  test(
    'reacts to session changes made outside of its own login/register/logout',
    () async {
      var notified = false;
      viewModel.addListener(() => notified = true);

      await sessionStore.setSession(fakeSession('external'));

      expect(notified, isTrue);
      expect(viewModel.isAuthenticated, isTrue);
    },
  );

  test(
    'logout still signs out, without an unhandled error, when the server call fails',
    () async {
      final failing = FakeAuthRepository(logoutError: const NetworkException());
      viewModel = AuthViewModel(
        failing,
        AuthSessionStore(failing, InMemoryTokenStore()),
        webServerSettings(),
      );
      await viewModel.login('alice', 'a valid password');

      await viewModel.logout();
      // Let the fire-and-forget revoke run; an uncaught error would fail the test.
      await pumpEventQueue();

      expect(viewModel.isAuthenticated, isFalse);
    },
  );

  group('on a native build', () {
    late InMemoryServerOriginStore originStore;
    late ServerSettings serverSettings;

    setUp(() {
      originStore = InMemoryServerOriginStore();
      serverSettings = ServerSettings(
        originStore,
        buildTimeOrigin: '',
        isWeb: false,
      );
      viewModel = AuthViewModel(repository, sessionStore, serverSettings);
    });

    test('asks for a server, with none filled in yet', () {
      expect(viewModel.canChangeServer, isTrue);
      expect(viewModel.serverOrigin, isEmpty);
    });

    test('rejects an unusable address without contacting a server', () async {
      await viewModel.login('alice', 'a valid password', serverAddress: ' ');

      expect(viewModel.errorMessage, AuthViewModel.invalidServerMessage);
      expect(repository.loginCalls, isEmpty);
      expect(viewModel.isSubmitting, isFalse);
    });

    test('signs in against the typed server and remembers it', () async {
      await viewModel.login(
        'alice',
        'a valid password',
        serverAddress: 'weaver.example.ts.net',
      );

      expect(viewModel.isAuthenticated, isTrue);
      expect(serverSettings.baseUrl, 'https://weaver.example.ts.net/api');
      expect(originStore.origin, 'https://weaver.example.ts.net');
    });

    test('registers against the typed server too', () async {
      await viewModel.register(
        'bob',
        'a valid password',
        serverAddress: 'https://weaver.example.ts.net',
      );

      expect(repository.registerCalls, ['bob']);
      expect(originStore.origin, 'https://weaver.example.ts.net');
    });

    test('does not remember a server that sign-in failed against', () async {
      viewModel = AuthViewModel(
        FakeAuthRepository(
          loginError: const ApiException('bad creds', statusCode: 401),
        ),
        sessionStore,
        serverSettings,
      );

      await viewModel.login(
        'alice',
        'wrong password',
        serverAddress: 'https://weaver.example.ts.net',
      );

      expect(originStore.origin, isNull);
    });

    test(
      'an unreachable server says so instead of blaming the password',
      () async {
        viewModel = AuthViewModel(
          FakeAuthRepository(loginError: const NetworkException()),
          sessionStore,
          serverSettings,
        );

        await viewModel.login(
          'alice',
          'a valid password',
          serverAddress: 'https://typo.example',
        );

        expect(viewModel.errorMessage, const NetworkException().message);
      },
    );

    test('restoreSession prefills the remembered server', () async {
      originStore.origin = 'https://weaver.example.ts.net';

      await viewModel.restoreSession();

      expect(viewModel.serverOrigin, 'https://weaver.example.ts.net');
    });

    test(
      'restoreSession skips the refresh when no server has been chosen',
      () async {
        final tokenStore = InMemoryTokenStore()..token = 'orphaned';
        // FakeAuthRepository.refresh throws, so reaching it fails the test.
        viewModel = AuthViewModel(
          repository,
          AuthSessionStore(repository, tokenStore),
          serverSettings,
        );

        await viewModel.restoreSession();

        expect(viewModel.hasRestored, isTrue);
        expect(viewModel.isAuthenticated, isFalse);
      },
    );
  });
}
