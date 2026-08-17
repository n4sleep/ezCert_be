---
layout: ModuleUnit
title: AWS pricing and billing
canonicalUrl: https://docs.aws.amazon.com/whitepapers/latest/how-aws-pricing-works/welcome.html
description: AWS pricing models, free tier, consolidated billing, and support plans
---

# AWS pricing and billing

## Pay-as-you-go and pricing models

AWS pricing follows a pay-as-you-go model: you pay only for what you use, with no upfront commitments and no termination fees. On-demand pricing lets you pay for compute or storage per unit of consumption, for example per second of EC2 usage or per GB of S3 storage. Reserved capacity, such as Reserved Instances and Savings Plans, offers lower prices in exchange for a one- or three-year commitment. Spot pricing allows you to use unused capacity at a discount. Pricing varies by region and by service, and data transfer out of AWS is generally charged while inbound transfer is free.

## AWS Free Tier

The AWS Free Tier gives new customers a limited amount of usage at no cost. It includes three types of offers: always-free services, such as 1 million Lambda requests per month; 12-month free offers, such as 750 hours of a t2.micro EC2 instance per month; and short-term trials such as Amazon SageMaker. The Free Tier helps you explore AWS services without cost, but usage beyond the limits is billed normally, so it is important to monitor usage.

## Consolidated billing and cost management

With AWS Organizations you can create multiple accounts under a management account and use consolidated billing, which combines usage across accounts into a single bill. Consolidated billing lets you share Reserved Instances and Savings Plans across accounts and benefits from volume pricing. AWS Cost Explorer provides charts and reports of your cost and usage, AWS Budgets lets you set custom budgets and alerts, and billing alarms in Amazon CloudWatch warn you when estimated charges exceed a threshold. The AWS Pricing Calculator estimates the cost of planned workloads.

## AWS Support plans

AWS offers several support plans with different levels of access and response times. Basic support is free and includes documentation, whitepapers, and access to AWS Trusted Advisor checks. Developer support includes business-hours email access to Cloud Support Associates. Business support adds 24/7 phone and chat access and Trusted Advisor full checks. Enterprise support adds a Technical Account Manager, a designated Solutions Architect, and 15-minute response for business-critical systems. Support plans are a cost consideration when planning AWS adoption.
