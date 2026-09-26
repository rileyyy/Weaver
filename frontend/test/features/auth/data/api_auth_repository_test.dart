import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/data/api_auth_repository.dart';
import 'package:weaver/shared/models/user.dart';

http.Response _jsonResponse(Object body, {int statusCode = 200}) =>
    http.Response(
      jsonEncode(body),
      statusCode,
      headers: {'content-type': 'application/json'},
    );

Map<String, dynamic> _authBody({String kind = 'Human'}) => {
  'accessToken': 'access-123',
  'accessTokenExpiresAtUtc': '2026-01-01T00:15:00Z',
  'refreshToken': 'refresh-456',
  'user': {'id': 'user-1', 'username': 'alice', 'kind': kind},
};

void main() {
  const baseUrl = 'http://backend.test/api';

  test('login posts credentials and parses the returned session', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_authBody());
    });

    final repository = ApiAuthRepository(client, baseUrl);
    final session = await repository.login('alice', 'a valid password');

    expect(sentRequest!.url.path, '/api/auth/login');
    expect(jsonDecode(sentRequest!.body), {
      'username': 'alice',
      'password': 'a valid password',
    });
    expect(session.accessToken, 'access-123');
    expect(session.refreshToken, 'refresh-456');
    expect(session.user.username, 'alice');
  });

  test(
    'login throws an ApiException with the problem detail on failure',
    () async {
      final client = MockClient((request) async {
        return _jsonResponse({
          'detail': 'Invalid username or password.',
        }, statusCode: 401);
      });
      final repository = ApiAuthRepository(client, baseUrl);

      await expectLater(
        repository.login('alice', 'wrong'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            contains('Invalid username or password.'),
          ),
        ),
      );
    },
  );

  test('register posts credentials to the register endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_authBody());
    });

    final repository = ApiAuthRepository(client, baseUrl);
    await repository.register('alice', 'a valid password');

    expect(sentRequest!.url.path, '/api/auth/register');
  });

  test('refresh posts the refresh token to the refresh endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_authBody());
    });

    final repository = ApiAuthRepository(client, baseUrl);
    await repository.refresh('old-refresh-token');

    expect(sentRequest!.url.path, '/api/auth/refresh');
    expect(jsonDecode(sentRequest!.body), {
      'refreshToken': 'old-refresh-token',
    });
  });

  test('logout posts the refresh token to the logout endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 204);
    });

    final repository = ApiAuthRepository(client, baseUrl);
    await repository.logout('a-refresh-token');

    expect(sentRequest!.url.path, '/api/auth/logout');
    expect(jsonDecode(sentRequest!.body), {'refreshToken': 'a-refresh-token'});
  });

  test('parses an Agent user kind', () async {
    final client = MockClient(
      (request) async => _jsonResponse(_authBody(kind: 'Agent')),
    );
    final repository = ApiAuthRepository(client, baseUrl);

    final session = await repository.login('bot', 'a valid password');

    expect(session.user.kind, UserKind.agent);
  });
}
