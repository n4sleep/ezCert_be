---
layout: ModuleUnit
title: AWS global infrastructure - Cloud Practitioner
canonicalUrl: https://docs.aws.amazon.com/whitepapers/latest/aws-overview/global-infrastructure.html
description: AWS Regions, Availability Zones, and edge locations
---

# AWS global infrastructure

## Regions

An AWS Region is a geographically distinct area that contains multiple Availability Zones. Each region is independent and isolated from the other regions, so a failure in one region does not affect the others. Customers choose regions for data residency and compliance requirements, latency to end users, and pricing, since prices can vary between regions. Not every service is available in every region.

## Availability Zones

An Availability Zone (AZ) consists of one or more data centers that are physically separated from other AZs, each with independent power, networking, and cooling. AZs within a region are connected through redundant, low-latency links. This design lets customers architect applications that run across multiple AZs, so an application can remain available if an entire AZ fails. Placing resources in multiple AZs is the foundation of high availability and fault tolerance on AWS.

## Edge locations

Edge locations are sites that AWS maintains to cache content closer to end users, primarily for services such as Amazon CloudFront, a content delivery network. When users request content, CloudFront serves it from the nearest edge location, reducing latency. Edge locations are not AZs; they support caching and acceleration rather than running your compute workloads.

## Benefits of the AWS cloud

The AWS Cloud provides six key benefits for customers. Trade fixed expense for variable expense, so you pay only for what you consume instead of making large upfront investments in data centers. Benefit from massive economies of scale, because AWS aggregates usage from many customers, prices stay lower than most single-tenant infrastructure. Stop guessing capacity: scale up or down based on demand. Increase speed and agility, since new resources are available in minutes instead of months. Stop spending money on running and maintaining data centers, and instead focus on applications and customers. Go global in minutes, deploying applications to multiple regions around the world.

## Scalability and elasticity

Scalability is the ability of a system to handle increased load by adding resources. Elasticity is the ability to scale resources up or down as demand changes, so you pay only for what you need. AWS supports both vertical scaling, such as moving to a larger instance type, and horizontal scaling, such as adding more instances behind a load balancer. Auto scaling can add or remove capacity automatically based on demand, which is a core concept for the AWS Cloud Practitioner exam.
