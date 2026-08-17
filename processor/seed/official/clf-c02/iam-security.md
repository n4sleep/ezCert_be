---
layout: ModuleUnit
title: IAM and the shared responsibility model
canonicalUrl: https://docs.aws.amazon.com/IAM/latest/UserGuide/introduction.html
description: AWS Identity and Access Management, users, groups, roles, policies
---

# AWS Identity and Access Management (IAM)

## What is IAM

AWS Identity and Access Management (IAM) is a web service that helps you securely control access to AWS resources. IAM lets you manage who is authenticated (signed in) and who is authorized (has permissions) to use resources. You create users, groups, and roles, and attach policies that grant or deny specific permissions.

## Users, groups, and roles

An IAM user is an entity that represents a person or service that interacts with AWS; users have long-term credentials such as passwords or access keys. An IAM group is a collection of users, so you can manage permissions for many users at once by attaching policies to the group instead of to each user. An IAM role is an entity that you assume temporarily; roles have no long-term credentials. AWS services such as EC2 assume roles to gain permissions, and federated users assume roles instead of having permanent credentials. Using roles instead of access keys is a security best practice.

## Policies and least privilege

IAM policies are JSON documents that define permissions: an effect (allow or deny), the action, and the resource. The recommended practice is least privilege, granting only the permissions required to perform a task. AWS evaluates all policies for a request, and an explicit deny always overrides an allow. Managed policies are AWS-provided policies you can attach, while customer managed policies and inline policies are written by you.

## Security best practices

Security best practices for IAM include locking away the AWS account root user credentials and enabling multi-factor authentication (MFA) on the root account and all users. Use IAM roles instead of long-term access keys wherever possible, rotate credentials regularly, and avoid putting credentials in application code. AWS CloudTrail records API activity in your account, providing an audit trail of who did what.

## The shared responsibility model

Security in the cloud is a shared responsibility between AWS and the customer. AWS is responsible for security of the cloud: the physical facilities, hardware, software, and networking that run AWS services. The customer is responsible for security in the cloud: configuring services, managing guest operating systems and firewall rules, protecting data with encryption, and managing access with IAM. The split depends on the service: for infrastructure services such as EC2 the customer manages more, while for managed services such as S3 and Lambda AWS manages more of the stack.
