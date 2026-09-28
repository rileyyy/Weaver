import 'package:weaver/core/network/api_exception.dart';

/// The server's own message for the refusals an admin can act on (invalid
/// input, not an admin, gone, or a rule like "still in use"); [fallback]
/// for anything else, such as the server being unreachable.
String adminErrorMessage(ApiException error, String fallback) =>
    switch (error.statusCode) {
      400 || 403 || 404 || 409 => error.message,
      _ => fallback,
    };
