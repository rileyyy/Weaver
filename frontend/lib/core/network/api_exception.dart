/// Thrown by repository implementations when a backend request fails.
class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;

  /// The HTTP status of the failed response, when there was one.
  final int? statusCode;

  bool get isConflict => statusCode == 409;

  @override
  String toString() => message;
}
