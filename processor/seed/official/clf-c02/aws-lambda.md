---
layout: ModuleUnit
title: AWS Lambda - serverless compute
canonicalUrl: https://docs.aws.amazon.com/lambda/latest/dg/welcome.html
description: AWS Lambda functions, event-driven invocation, and scaling
---

# AWS Lambda

## What is AWS Lambda

AWS Lambda is a serverless compute service that runs your code in response to events without you provisioning or managing servers. You upload your code as a Lambda function, and Lambda runs it only when it is invoked, scaling automatically to the volume of requests. There is no charge when your code is not running, which is why Lambda is described as paying only for what you use.

## Event-driven invocation

Lambda functions are invoked by events from many AWS services. For example, an object upload to Amazon S3 can trigger a function to process the object, an update to an Amazon DynamoDB table can trigger a function to react to the change, and Amazon API Gateway can invoke a function for every HTTP request to a REST API. Other common event sources include Amazon Simple Notification Service (SNS) and Amazon Simple Queue Service (SQS). This event-driven model is central to building serverless applications.

## Scaling and billing

Lambda scales automatically: each invocation runs in its own isolated environment, and the service adds capacity as concurrency increases. You set a concurrency limit to protect downstream resources. Billing is based on the number of invocations and the duration your code runs, measured in GB-seconds, which is memory allocated multiplied by execution time. Because idle functions cost nothing, Lambda suits spiky and unpredictable workloads and is a good fit for short-running tasks; the service has a maximum execution timeout of 15 minutes.

## When to choose Lambda

Lambda is ideal for event-driven processing, such as image resizing, log processing, and real-time file transformation, and for building APIs with API Gateway in a serverless architecture. It removes the operational burden of patching and managing servers. For long-running or stateful workloads, or workloads with very predictable, high utilization, other compute options such as Amazon EC2 may be more cost-effective.
