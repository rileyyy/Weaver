import 'package:weaver/core/network/api_exception.dart';

/// The server answered, but not in the shape this app expects: usually a
/// backend contract change the app hasn't caught up with, not a network
/// problem.
class UnexpectedResponseException extends ApiException {
  const UnexpectedResponseException(String path, this.cause)
    : super('The server sent a response this app does not understand ($path).');

  final Object cause;
}
