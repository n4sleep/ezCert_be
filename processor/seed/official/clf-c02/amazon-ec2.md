---
layout: ModuleUnit
title: Amazon EC2 - virtual servers in the cloud
canonicalUrl: https://docs.aws.amazon.com/whitepapers/latest/aws-overview/amazon-ec2.html
description: Amazon EC2 instances, AMIs, storage, and pricing
---

# Amazon EC2

## What is Amazon EC2

Amazon Elastic Compute Cloud (Amazon EC2) provides resizable, secure compute capacity in the cloud. An EC2 instance is a virtual server running on AWS hardware. You choose an Amazon Machine Image (AMI), which defines the operating system and software configuration, and an instance type, which defines the CPU, memory, storage, and network capacity. Instance types are grouped into families such as general purpose, compute optimized, memory optimized, and storage optimized, so you can match the instance to the workload.

## Storage options

EC2 instances can use Amazon Elastic Block Store (EBS) volumes, which are durable, block-level storage volumes attached to an instance and independent of the instance lifecycle. Instance store volumes provide temporary, high-performance storage that is physically attached to the host and is lost when the instance stops or terminates. For shared file storage across instances, Amazon Elastic File System (EFS) provides a scalable network file system.

## Pricing models

EC2 offers several purchasing options. On-Demand instances let you pay for compute capacity by the second with no long-term commitment, which suits unpredictable workloads. Reserved Instances and Savings Plans provide significant discounts in exchange for a commitment to a consistent amount of usage over one or three years. Spot Instances let you request unused EC2 capacity at a discounted price, and AWS can interrupt them with two minutes of warning, making them suitable for fault-tolerant or flexible workloads. Dedicated Hosts provide a physical server fully dedicated to your use.

## Elasticity and integration

EC2 integrates with Elastic Load Balancing (ELB) to distribute traffic across instances and with Amazon EC2 Auto Scaling to automatically add or remove instances in response to demand. Security groups act as a virtual firewall for instances, controlling inbound and outbound traffic. EC2 also supports creating custom AMIs so you can launch identical instances from a known configuration, which speeds up deployment and testing.
