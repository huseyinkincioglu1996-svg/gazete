class HttpError extends Error {
  constructor(statusCode, message, details) {
    super(message);
    this.name = 'HttpError';
    this.statusCode = statusCode;
    this.details = details;
  }
}

function asyncHandler(handler) {
  return (req, res, next) => Promise.resolve(handler(req, res, next)).catch(next);
}

function isDuplicateKeyError(error) {
  return error && (error.code === 11000 || error.code === 11001);
}

module.exports = {
  HttpError,
  asyncHandler,
  isDuplicateKeyError
};
