---
layout: ModuleUnit
title: Amazon S3 - object storage
canonicalUrl: https://docs.aws.amazon.com/whitepapers/latest/aws-overview/amazon-s3.html
description: Amazon S3 buckets, objects, durability, and storage classes
---

# Amazon S3

## What is Amazon S3

Amazon Simple Storage Service (Amazon S3) is an object storage service that stores data as objects within buckets. An object consists of the data, a key that uniquely identifies it in the bucket, and metadata. Buckets have a globally unique name and are created in a specific AWS Region. S3 is designed for 99.999999999% (11 nines) durability, which means you can reliably store and retrieve any amount of data, from a single file to exabytes, for virtually any use case.

## Core features

S3 supports versioning, which keeps multiple versions of an object so you can recover from accidental deletion or overwrites. Lifecycle policies automatically transition objects to less expensive storage classes or delete them after a defined period. Server-side encryption protects data at rest, and S3 integrates with IAM policies, bucket policies, and access control lists to control who can access objects. You can host a static website from a bucket and use S3 as the origin for Amazon CloudFront.

## Storage classes

S3 offers multiple storage classes that trade price against retrieval characteristics. S3 Standard is for frequently accessed data. S3 Standard-Infrequent Access (S3 Standard-IA) lowers cost for data accessed less often but retrievable immediately. S3 Glacier Flexible Retrieval and S3 Glacier Deep Archive provide low-cost archival storage with retrieval times ranging from minutes to hours. S3 Intelligent-Tiering automatically moves objects between access tiers based on changing access patterns, without retrieval fees or operational overhead.

## Access management

Access to S3 is controlled by identity-based policies (IAM) and resource-based policies such as bucket policies. Bucket policies are JSON documents that grant or deny permissions at the bucket level and can be used to make objects public or restrict access to specific principals. Cross-region replication copies objects to buckets in other regions for compliance, latency, or disaster recovery purposes.
