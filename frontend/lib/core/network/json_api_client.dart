import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:weaver/core/network/api_endpoint.dart';
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
  JsonApiClient(this._http, this._endpoint);

  final http.Client _http;
  final ApiEndpoint _endpoint;

  static const Map<String, String> _jsonHeaders = {
    'Content-Type': 'application/json',
  };

  /// Each [query] value is a `String`, or an `Iterable<String>` to repeat
  /// its key once per value (`?tags=a&tags=b`), which is how the server
  /// binds a list input.
  Future<T> get<T>(
    String path,
    T Function(Object? json) decode, {
    Map<String, Object>? query,
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
    Map<String, String>? query,
    required String failureMessage,
  }) => _send(
    path,
    () => _http.put(
      _uri(path, query),
      headers: _jsonHeaders,
      body: jsonEncode(body),
    ),
    failureMessage,
    decode,
  );

  /// A partial update: the server leaves a field missing from [body]
  /// unchanged and clears one sent as null.
  Future<T> patch<T>(
    String path,
    Object? body,
    T Function(Object? json) decode, {
    required String failureMessage,
  }) => _send(
    path,
    () =>
        _http.patch(_uri(path), headers: _jsonHeaders, body: jsonEncode(body)),
    failureMessage,
    decode,
  );

  /// A POST whose response body the caller doesn't need.
  Future<void> postIgnoringBody(
    String path,
    Object? body, {
    required String failureMessage,
  }) => post(path, body, (_) {}, failureMessage: failureMessage);

  /// A PUT whose response body the caller doesn't need.
  Future<void> putIgnoringBody(
    String path,
    Object? body, {
    required String failureMessage,
  }) => put(path, body, (_) {}, failureMessage: failureMessage);

  /// A PATCH whose response body the caller doesn't need.
  Future<void> patchIgnoringBody(
    String path,
    Object? body, {
    required String failureMessage,
  }) => patch(path, body, (_) {}, failureMessage: failureMessage);

  Future<void> delete(
    String path, {
    Map<String, String>? query,
    required String failureMessage,
  }) => deleteReturning(
    path,
    (_) {},
    query: query,
    failureMessage: failureMessage,
  );

  /// A DELETE whose response body the caller needs, e.g. removing a user's
  /// lockout answers with the user.
  Future<T> deleteReturning<T>(
    String path,
    T Function(Object? json) decode, {
    Map<String, String>? query,
    required String failureMessage,
  }) => _send(
    path,
    () => _http.delete(_uri(path, query)),
    failureMessage,
    decode,
  );

  /// A GET of a file's content rather than JSON, such as a download.
  Future<Uint8List> getBytes(String path, {required String failureMessage}) =>
      _checkedResponse(
        () => _http.get(_uri(path)),
        failureMessage,
      ).then((response) => response.bodyBytes);

  /// A `multipart/form-data` POST carrying [files], for uploads.
  Future<T> postMultipart<T>(
    String path,
    List<http.MultipartFile> files,
    T Function(Object? json) decode, {
    required String failureMessage,
  }) => _send(
    path,
    () async {
      final request = http.MultipartRequest('POST', _uri(path))
        ..files.addAll(files);
      return http.Response.fromStream(await _http.send(request));
    },
    failureMessage,
    decode,
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
    final response = await _checkedResponse(request, failureMessage);
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

  /// Sends [request] and returns its response if it succeeded.
  Future<http.Response> _checkedResponse(
    Future<http.Response> Function() request,
    String failureMessage,
  ) async {
    final http.Response response;
    try {
      response = await request();
      // Not just http.ClientException: on native, IOClient wraps socket
      // errors but lets TLS failures (e.g. an untrusted certificate) escape
      // as dart:io's HandshakeException, which can't be named here because
      // this file is also compiled for web.
    } on Exception {
      throw const NetworkException();
    }

    if (response.statusCode < 200 || response.statusCode >= 300) {
      final detail = _problemDetail(response);
      throw ApiException(
        '${detail ?? failureMessage} (${response.statusCode}).',
        statusCode: response.statusCode,
        detail: detail,
      );
    }
    return response;
  }

  Uri _uri(String path, [Map<String, Object>? query]) {
    final uri = Uri.parse('${_endpoint.baseUrl}$path');
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
