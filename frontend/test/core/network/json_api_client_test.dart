import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/core/network/network_exception.dart';
import 'package:weaver/core/network/unexpected_response_exception.dart';

const _baseUrl = 'http://backend.test/api';

JsonApiClient _client(MockClientHandler handler) =>
    JsonApiClient(MockClient(handler), _baseUrl);

void main() {
  test('get decodes the body and sends query parameters', () async {
    Uri? sent;
    final api = _client((request) async {
      sent = request.url;
      return http.Response(
        jsonEncode([
          {'id': 'a'},
        ]),
        200,
      );
    });

    final ids = await api.get(
      '/work-items',
      (json) => JsonApiClient.listOf(json, (item) => item['id'] as String),
      query: {'parentId': 'p 1'},
      failureMessage: 'Failed',
    );

    expect(ids, ['a']);
    expect(sent.toString(), '$_baseUrl/work-items?parentId=p+1');
  });

  test('post sends JSON with a content type', () async {
    http.Request? sent;
    final api = _client((request) async {
      sent = request;
      return http.Response('', 204);
    });

    await api.postIgnoringBody('/x', {'a': 1}, failureMessage: 'Failed');

    expect(sent!.headers['Content-Type'], startsWith('application/json'));
    expect(jsonDecode(sent!.body), {'a': 1});
  });

  test(
    'a non-2xx response throws ApiException with the problem detail and status',
    () async {
      final api = _client(
        (_) async =>
            http.Response(jsonEncode({'detail': 'Title is required.'}), 400),
      );

      await expectLater(
        api.postIgnoringBody('/x', {}, failureMessage: 'Failed'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 400)
              .having((e) => e.message, 'message', 'Title is required. (400).'),
        ),
      );
    },
  );

  test('a non-JSON error body falls back to the failure message', () async {
    final api = _client((_) async => http.Response('<html>oops</html>', 502));

    await expectLater(
      api.delete('/x', failureMessage: 'Failed to delete'),
      throwsA(
        isA<ApiException>().having(
          (e) => e.message,
          'message',
          'Failed to delete (502).',
        ),
      ),
    );
  });

  test('an unreachable server throws NetworkException', () async {
    final api = _client((_) async => throw http.ClientException('refused'));

    await expectLater(
      api.get('/x', (json) => json, failureMessage: 'Failed'),
      throwsA(isA<NetworkException>()),
    );
  });

  test(
    'a body in the wrong shape throws UnexpectedResponseException',
    () async {
      final api = _client(
        (_) async => http.Response(jsonEncode({'id': 1}), 200),
      );

      await expectLater(
        api.get(
          '/x',
          (json) => (json as Map<String, dynamic>)['id'] as String,
          failureMessage: 'Failed',
        ),
        throwsA(isA<UnexpectedResponseException>()),
      );
    },
  );

  test('malformed JSON throws UnexpectedResponseException', () async {
    final api = _client((_) async => http.Response('{not json', 200));

    await expectLater(
      api.get('/x', (json) => json, failureMessage: 'Failed'),
      throwsA(isA<UnexpectedResponseException>()),
    );
  });
}
