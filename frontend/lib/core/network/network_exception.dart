import 'package:weaver/core/network/api_exception.dart';

/// The server couldn't be reached at all (offline, DNS, connection refused).
class NetworkException extends ApiException {
  const NetworkException()
    : super('Could not reach the server. Check your connection and try again.');
}
