---
layout: ModuleUnit
title: Amazon VPC - networking in the cloud
canonicalUrl: https://docs.aws.amazon.com/whitepapers/latest/aws-overview/amazon-vpc.html
description: Amazon VPC, subnets, security groups, and network ACLs
---

# Amazon VPC

## What is Amazon VPC

Amazon Virtual Private Cloud (Amazon VPC) lets you provision a logically isolated section of the AWS Cloud where you launch AWS resources. You define the network by choosing an IP address range in CIDR notation, such as 10.0.0.0/16. Inside the VPC you create subnets, which are segments of the IP address range placed in Availability Zones. Resources such as EC2 instances launch into subnets.

## Public and private subnets

Subnets that route traffic to an internet gateway are called public subnets and can host resources that need direct internet access, such as web servers. Subnets without a route to the internet are private subnets and typically host databases or application tiers. An internet gateway (IGW) provides the connection between the VPC and the internet, while a NAT gateway allows instances in private subnets to initiate outbound internet traffic, for example to download updates, without being reachable from the internet.

## Security groups vs network ACLs

Security groups act as a virtual firewall for an instance or other resource, controlling inbound and outbound traffic. They are stateful: if you allow inbound traffic, the response is automatically allowed outbound. By default, security groups deny all inbound traffic and allow all outbound traffic. Network access control lists (NACLs) act as a firewall at the subnet level and are stateless, so you must define rules for both directions. NACLs support both allow and deny rules and are evaluated in order by rule number.

## High availability design

To be resilient, place resources in multiple subnets across multiple Availability Zones. If one AZ becomes unavailable, the application continues to run in the other. Combined with Elastic Load Balancing and Auto Scaling, a multi-AZ VPC design is the standard pattern for highly available applications on AWS and a core topic for the Cloud Practitioner exam.
