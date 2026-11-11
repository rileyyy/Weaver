import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/features/board/data/api_calendar_repository.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

void main() {
  test('today reads the server date as a local calendar day', () async {
    final repository = ApiCalendarRepository(
      MockClient((request) async {
        expect(request.method, 'GET');
        expect(request.url.path, '/api/calendar/today');
        return http.Response(
          jsonEncode({'today': '2026-11-11'}),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
      const FixedApiEndpoint('http://host/api'),
    );

    expect(await repository.today(), DateTime(2026, 11, 11));
  });
}
