/// Thrown by repositories when a backend request fails. View models catch
/// this type (and its subclasses) rather than everything, so a programming
/// error still surfaces instead of being shown as "check your connection".
class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;

  /// The HTTP status of the failed response, when there was one.
  final int? statusCode;

  bool get isConflict => statusCode == 409;

  bool get isUnauthorized => statusCode == 401;

  @override
  String toString() => message;
}
