import { Request, Response, NextFunction } from 'express';
import { ServicesContainer } from './services/container';
import { RequestWithPublisher } from './types';

// High-order function to inject ServiceContainer into middleware
// Express middleware used to authenticate our client requested user token
export const extractPublisher = (services: ServicesContainer) => (req: Request, res: Response, next: NextFunction) => {
  const token = getTokenFromHeader(req);
  let publisherId = '';

  if (token === undefined) {
    // LOCAL DEV MODE: If REQUIRE_AUTH=false and no token, check for publisherId in query or use config default
    if (!services.config.requireAuth) {
      publisherId = (req.query.publisherId as string) || services.config.publisherId || 'DefaultPublisher';
      (req as RequestWithPublisher).publisherId = publisherId;
      next();
      return;
    }

    if (req.query.publisherId === undefined || req.query.publisherId === '') {
      res.status(401).send('Either a bearer token in the header or a PublisherId query string parameter is required.');
      return;
    }

    publisherId = req.query.publisherId as string;
  } else {
    const decoded = services.jwt.decodeToken(token);

    // LOCAL DEV MODE: If token decode fails and REQUIRE_AUTH=false, use config publisherId
    if (decoded === null || decoded === undefined) {
      if (!services.config.requireAuth) {
        publisherId = services.config.publisherId || 'DefaultPublisher';
        (req as RequestWithPublisher).publisherId = publisherId;
        next();
        return;
      }
      res.status(401).send('Invalid bearer token.');
      return;
    }

    if (decoded.tid === undefined || decoded.appid === undefined) {
      res.status(401).send('TenantId & AppId required in token.');
      return;
    }

    publisherId = `${decoded.tid as string}${decoded.appid as string}`;
  }

  // Store the publisher id on the request for later middleware/handlers
  (req as RequestWithPublisher).publisherId = publisherId;

  next();
};

const getTokenFromHeader: (req: Request) => string | undefined = (req) => {
  const authHeader = req.headers.authorization;

  if (authHeader === undefined) {
    return undefined;
  }

  if (!authHeader.toLowerCase().startsWith('bearer ')) {
    return undefined;
  }

  const parts = authHeader.split(' ');

  if (parts.length < 1) {
    return undefined;
  }

  return parts[1];
};
