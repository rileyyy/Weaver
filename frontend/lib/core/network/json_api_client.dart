import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/network_exception.dart';
import 'package:weaver/core/network/unexpected_response_exception.dart';

/// The one place repositories turn HTTP into JSON: building URIs, checking
/// status codes, reading the server's problem-detail message, and decoding.
/// Every failure comes out as an [ApiException]: a non-2xx response keeps
/// its status code, an unreachable server is a [NetworkException], and a
/// body that doesn't match what [decode] expects is an
/// [UnexpectedResponseException].
class JsonApiClient {
  JsonApiClient(this._http, this._baseUrl);

  final http.Client _http;
  final String _baseUrl;

  static const Map<String, String> _jsonHeaders = {
    'Content-Type': 'application/json',
  };

  Future<T> get<T>(
    String path,
    T Function(Object? json) decode, {
    Map<String, String>? query,
    required String failureMessage,
  }) => _send(path, () => _http.get(_uri(path, query)), failureMessage, decode);

  Future<T> post<T>(
    String path,
    Object? body,
    T Function(Object? json) decode, {
    required String failureMessage,
  }) => _send(
    path,
    () => _http.post(_uri(path), headers: _jsonHeaders, body: jsonEncode(body)),
    failureMessage,
    decode,
  );

  Future<T> put<T>(
    String path,
    Object? body,
    T Function(Object? json) decode, {
    required String failureMessage,
  }) => _send(
    path,
    () => _http.put(_uri(path), headers: _jsonHeaders, body: jsonEncode(body)),
    failureMessage,
    decode,
  );

  /// A POST whose response body the caller doesn't need.
  Future<void> postIgnoringBody(
    String path,
    Object? body, {
    required String failureMessage,
  }) => post(path, body, (_) {}, failureMessage: failureMessage);

  Future<void> delete(
    String path, {
    Map<String, String>? query,
    required String failureMessage,
  }) => _send(
    path,
    () => _http.delete(_uri(path, query)),
    failureMessage,
    (_) {},
  );

  /// Decodes a JSON array of objects with [fromJson].
  static List<T> listOf<T>(
    Object? json,
    T Function(Map<String, dynamic> item) fromJson,
  ) => [
    for (final item in json as List<dynamic>)
      fromJson(item as Map<String, dynamic>),
  ];

  Future<T> _send<T>(
    String path,
    Future<http.Response> Function() request,
    String failureMessage,
    T Function(Object? json) decode,
  ) async {
    final http.Response response;
    try {
      response = await request();
    } on http.ClientException {
      throw const NetworkException();
    }

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(
        '${_problemDetail(response) ?? failureMessage} (${response.statusCode}).',
        statusCode: response.statusCode,
      );
    }

    try {
      return decode(response.body.isEmpty ? null : jsonDecode(response.body));
      // A cast or format failure here means the response doesn't match the
      // contract; report it as such instead of letting a TypeError escape.
    } on TypeError catch (e) {
      throw UnexpectedResponseException(path, e);
    } on FormatException catch (e) {
      throw UnexpectedResponseException(path, e);
    }
  }

  Uri _uri(String path, [Map<String, String>? query]) {
    final uri = Uri.parse('$_baseUrl$path');
    return query == null ? uri : uri.replace(queryParameters: query);
  }

  String? _problemDetail(http.Response response) {
    try {
      final body = jsonDecode(response.body);
      return body is Map<String, dynamic> ? body['detail'] as String? : null;
    } on FormatException {
      return null;
    }
  }
}
